using Xunit;

public class SoapParserTests
{
    [Fact]
    public void Parse_ReturnsCpfAndStatus_WhenSoapContainsValues()
    {
        var soap = "<Envelope><Body><BiometriaResponse><cpf>12345678901</cpf><status>OK</status></BiometriaResponse></Body></Envelope>";
        var (cpf, status) = SoapParser.Parse(soap);
        Assert.Equal("12345678901", cpf);
        Assert.Equal("OK", status);
    }
}
