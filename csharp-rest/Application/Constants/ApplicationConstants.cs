namespace CsharpRest.Application.Constants;

public static class ConfigKeys
{
    public const string SoapBaseUrl = "SOAP_BASE_URL";
}

public static class HttpClientNames
{
    public const string SoapClient = "soapClient";
}

public static class Headers
{
    public const string CorrelationId = "X-Correlation-Id";
}

public static class MediaTypes
{
    public const string Xml = "text/xml";
}

public static class SoapConstants
{
    public const string BaseUrlLocal = "http://localhost:8081";
    public const string ServicePath = "/soap/biometria.svc";
    public const string SoapAction = "\"http://tempuri.org/IBiometriaService/GetBiometria\"";
    public const string SoapNamespace = "http://tempuri.org/";
    public const string EnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";
    public const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";
    public const string XsdNamespace = "http://www.w3.org/2001/XMLSchema";
    public const string SoapEnvelopeTemplate = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
        "<soap:Envelope xmlns:soap=\"{0}\" xmlns:xsi=\"{1}\" xmlns:xsd=\"{2}\">" +
        "<soap:Body><GetBiometria xmlns=\"{3}\"><cpf>{4}</cpf></GetBiometria></soap:Body></soap:Envelope>";
}

public static class PolicyConstants
{
    public const int RetryCount = 3;
    public const int TooManyRequestsStatusCode = 429;
}

public static class ResultKeys
{
    public const string CorrelationId = "correlationId";
}

public static class Strings
{
    public const string UnableToFetchBiometria = "Unable to fetch biometria";
    public const string NotFoundClient = "Not found";
    public const string BiometriaNotFound = "Biometria not found";
    public const string Fallback = "Fallback";
    public const string Unknown = "unknown";
}

public static class StatusCodes
{
    public const string NotFound = "NOT_FOUND";
}
