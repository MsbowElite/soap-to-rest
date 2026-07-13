using System.Net.Http.Headers;
using CsharpRest.Application.Constants;
using CsharpRest.Application.Interfaces;
using Polly;

namespace CsharpRest.Infrastructure.Clients;

public class SoapBiometriaClient : ISoapBiometriaClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IAsyncPolicy<System.Net.Http.HttpResponseMessage> _policy;
    private readonly ILoggerService _logger;

    public SoapBiometriaClient(IHttpClientFactory httpFactory, ILoggerService logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;

        var retry = Policy.HandleResult<System.Net.Http.HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(PolicyConstants.RetryCount, i => TimeSpan.FromSeconds(Math.Pow(2, i)), (outcome, timespan, retryCount, ctx) =>
            {
                _logger.Warning("[SOAP Client Policy] Retry {Retry} after {Delay} due to {Status}", retryCount, timespan, outcome.Result?.StatusCode);
            });

        var fallback = Policy<System.Net.Http.HttpResponseMessage>
            .Handle<Exception>()
            .FallbackAsync(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = Strings.Fallback
            }, async (outcome, ctx) =>
            {
                if (outcome.Exception != null)
                {
                    _logger.Error(outcome.Exception, "[SOAP Client Policy] Fallback triggered due to exception");
                }
                await Task.CompletedTask;
            });

        _policy = Policy.WrapAsync(fallback, retry);
    }

    public async Task<System.Net.Http.HttpResponseMessage> SendBiometriaRequestAsync(string cpf)
    {
        var client = _httpFactory.CreateClient(HttpClientNames.SoapClient);

        _logger.Debug("[SOAP Client] Preparing SOAP request for {Cpf}", cpf);

        var soap = string.Format(
            SoapConstants.SoapEnvelopeTemplate,
            SoapConstants.EnvelopeNamespace,
            SoapConstants.XsiNamespace,
            SoapConstants.XsdNamespace,
            SoapConstants.SoapNamespace,
            cpf);

        var contentMsg = new System.Net.Http.StringContent(soap, System.Text.Encoding.UTF8, MediaTypes.Xml);
        contentMsg.Headers.Add("SOAPAction", SoapConstants.SoapAction);

        return await _policy.ExecuteAsync(() => client.PostAsync(SoapConstants.ServicePath, contentMsg));
    }
}
