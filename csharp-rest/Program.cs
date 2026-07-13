using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using Serilog.Context;
using CsharpRest.Application.Constants;
using CsharpRest.Application.Interfaces;
using CsharpRest.Application.Models;
using CsharpRest.Application.Validators;
using CsharpRest.Application.Services;
using CsharpRest.Infrastructure.Clients;
using CsharpRest.Infrastructure.Logging;
using CsharpRest.Infrastructure.Mappers;
using CsharpRest.Infrastructure.Middleware;
using CsharpRest.Infrastructure.Repositories;
using CsharpRest.Domain.Dto;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Serilog structured logging
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// register layers: repository, mapper, SOAP client, logger service and business service
builder.Services.AddSingleton<IBiometriaRepository, OracleMockRepository>();
builder.Services.AddSingleton<IBiometriaMapper, BiometriaMapper>();
builder.Services.AddScoped<ISoapBiometriaClient, SoapBiometriaClient>();
builder.Services.AddSingleton<ILoggerService, SerilogLoggerService>();
builder.Services.AddScoped<IBiometriaService, BiometriaService>();
// register validator for request-level validation (application layer)
builder.Services.AddScoped<IValidator<BiometriaRequest>, BiometriaRequestValidator>();

// HttpClient with Polly retry
var soapBase = builder.Configuration[ConfigKeys.SoapBaseUrl] ?? SoapConstants.BaseUrlLocal;
Log.Information("[Startup] SOAP_BASE_URL = {SoapBase}", soapBase);
builder.Services.AddHttpClient(HttpClientNames.SoapClient, client =>
{
    client.BaseAddress = new Uri(soapBase);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypes.Xml));
})
    .AddPolicyHandler(GetRetryPolicy());

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "UP" }));

app.MapGet("/api/v1/biometria/{cpf}", async (string cpf, IBiometriaService service, IValidator<BiometriaRequest> validator, HttpContext httpContext) =>
{
    try
    {
        var correlationId = httpContext.Items[Headers.CorrelationId]?.ToString() ?? Strings.Unknown;
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            // fail-fast validation in application layer
            var request = new BiometriaRequest { Cpf = cpf };
            var validation = await validator.ValidateAsync(request);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage);
                return Results.BadRequest(new { correlationId, errors });
            }

            var result = await service.GetBiometriaAsync(cpf);
            if (result.IsSuccess && result.Value is not null)
            {
                var dto = result.Value;
                return Results.Ok(new { correlationId, cpf = dto.Cpf, status = dto.Status, matchScore = dto.MatchScore });
            }

            if (result.IsNotFound)
            {
                return Results.NotFound(new { correlationId, message = result.ErrorMessage ?? Strings.NotFoundClient });
            }

            return Results.Problem(
                title: Strings.UnableToFetchBiometria,
                detail: result.ErrorMessage,
                extensions: new Dictionary<string, object?> { [ResultKeys.CorrelationId] = correlationId });
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error fetching biometria for {Cpf}", cpf);
        return Results.Problem(
            title: Strings.UnableToFetchBiometria,
            detail: ex.Message,
            extensions: new Dictionary<string, object?> { [ResultKeys.CorrelationId] = httpContext.Items[Headers.CorrelationId]?.ToString() ?? Strings.Unknown });
    }
});

app.Run();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => (int)msg.StatusCode == PolicyConstants.TooManyRequestsStatusCode)
        .WaitAndRetryAsync(PolicyConstants.RetryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

// supporting types moved to infrastructure layer
