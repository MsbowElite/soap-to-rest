using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SoapCore;
using System.ServiceModel;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IBiometriaService, BiometriaService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.UseSoapEndpoint<IBiometriaService>("/soap/biometria.svc", new SoapEncoderOptions(), SoapSerializer.XmlSerializer);
});

app.Run();

[ServiceContract(Namespace = "http://tempuri.org/")]
public interface IBiometriaService
{
    [OperationContract]
    string GetBiometria(string cpf);
}

public class BiometriaService : IBiometriaService
{
    public string GetBiometria(string cpf)
    {
        // Simular resposta SOAP em XML simples
        return $"<BiometriaResponse><cpf>{cpf}</cpf><status>OK</status><matchScore>0.92</matchScore></BiometriaResponse>";
    }
}
