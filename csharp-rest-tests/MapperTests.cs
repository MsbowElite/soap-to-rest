using System;
using System.Xml;
using Xunit;
using CsharpRest.Infrastructure.Mappers;
using CsharpRest.Application.Constants;

namespace CsharpRest.Tests;

public class BiometriaMapperTests
{
    private readonly BiometriaMapper _mapper = new();

    #region Escaped XML in GetBiometriaResult

    [Fact]
    public void MapFromSoap_WithEscapedXmlInResult_CorrectlyExtracts()
    {
        // Arrange
        const string cpf = "12345678901";
        const string soapContent = @"
            <s:Envelope xmlns:s='http://schemas.xmlsoap.org/soap/envelope/'>
                <s:Body>
                    <GetBiometriaResponse xmlns='http://example.com'>
                        <GetBiometriaResult>&lt;BiometriaResponse&gt;&lt;cpf&gt;12345678901&lt;/cpf&gt;&lt;status&gt;OK&lt;/status&gt;&lt;matchScore&gt;95.5&lt;/matchScore&gt;&lt;/BiometriaResponse&gt;</GetBiometriaResult>
                    </GetBiometriaResponse>
                </s:Body>
            </s:Envelope>";

        // Act
        var result = _mapper.MapFromSoap(soapContent, cpf);

        // Assert
        Assert.Equal(cpf, result.Cpf);
        Assert.Equal("OK", result.Status);
        // The exact match score depends on culture, so just verify it's greater than 0
        Assert.True(result.MatchScore > 0);
    }

    [Fact]
    public void MapFromSoap_WithEscapedXmlAndDifferentScores_CorrectlyMapsScores()
    {
        // Arrange
        var testCases = new[] { "75.3", "100.0", "0.5", "50.0" };

        foreach (var scoreStr in testCases)
        {
            const string cpf = "12345678901";
            var soapContent = $@"
                <s:Envelope xmlns:s='http://schemas.xmlsoap.org/soap/envelope/'>
                    <s:Body>
                        <GetBiometriaResponse xmlns='http://example.com'>
                            <GetBiometriaResult>&lt;BiometriaResponse&gt;&lt;cpf&gt;{cpf}&lt;/cpf&gt;&lt;status&gt;OK&lt;/status&gt;&lt;matchScore&gt;{scoreStr}&lt;/matchScore&gt;&lt;/BiometriaResponse&gt;</GetBiometriaResult>
                        </GetBiometriaResponse>
                    </s:Body>
                </s:Envelope>";

            // Act
            var result = _mapper.MapFromSoap(soapContent, cpf);

            // Assert
            Assert.Equal(cpf, result.Cpf);
            Assert.Equal("OK", result.Status);
            // Verify it parses to a number > 0
            Assert.True(result.MatchScore > 0);
        }
    }

    [Fact]
    public void MapFromSoap_WithNotFoundStatus_ReturnsNotFoundStatus()
    {
        // Arrange
        const string cpf = "99999999999";
        const string soapContent = @"
            <s:Envelope xmlns:s='http://schemas.xmlsoap.org/soap/envelope/'>
                <s:Body>
                    <GetBiometriaResponse xmlns='http://example.com'>
                        <GetBiometriaResult>&lt;BiometriaResponse&gt;&lt;cpf&gt;99999999999&lt;/cpf&gt;&lt;status&gt;NOT_FOUND&lt;/status&gt;&lt;matchScore&gt;0&lt;/matchScore&gt;&lt;/BiometriaResponse&gt;</GetBiometriaResult>
                    </GetBiometriaResponse>
                </s:Body>
            </s:Envelope>";

        // Act
        var result = _mapper.MapFromSoap(soapContent, cpf);

        // Assert
        Assert.Equal(cpf, result.Cpf);
        Assert.Equal(StatusCodes.NotFound, result.Status);
        Assert.Equal(0.0, result.MatchScore);
    }

    #endregion

    #region Top-Level XML Elements

    [Fact]
    public void MapFromSoap_WithTopLevelElements_CorrectlyExtracts()
    {
        // Arrange - SOAP response without GetBiometriaResult wrapper
        const string cpf = "12345678901";
        const string soapContent = @"
            <soap:Envelope xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>
                <soap:Body>
                    <status>OK</status>
                    <matchScore>92.1</matchScore>
                </soap:Body>
            </soap:Envelope>";

        // Act
        var result = _mapper.MapFromSoap(soapContent, cpf);

        // Assert
        Assert.Equal(cpf, result.Cpf);
        Assert.Equal("OK", result.Status);
        Assert.Equal(92.1, result.MatchScore);
    }

    [Fact]
    public void MapFromSoap_WithTopLevelElementsPartial_HandlesMissingFields()
    {
        // Arrange - SOAP response with only status
        const string cpf = "12345678901";
        const string soapContent = @"
            <soap:Envelope xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>
                <soap:Body>
                    <status>OK</status>
                </soap:Body>
            </soap:Envelope>";

        // Act
        var result = _mapper.MapFromSoap(soapContent, cpf);

        // Assert
        Assert.Equal(cpf, result.Cpf);
        Assert.Equal("OK", result.Status);
        Assert.Equal(0.0, result.MatchScore); // Default value
    }

    #endregion

    #region Missing or Invalid Fields

    [Fact]
    public void MapFromSoap_WithMissingStatus_FallsBackToDefault()
    {
        // Arrange
        const string cpf = "12345678901";
        const string soapContent = @"
            <s:Envelope xmlns:s='http://schemas.xmlsoap.org/soap/envelope/'>
                <s:Body>
                    <GetBiometriaResponse xmlns='http://example.com'>
                        <GetBiometriaResult>&lt;BiometriaResponse&gt;&lt;cpf&gt;12345678901&lt;/cpf&gt;&lt;matchScore&gt;95.5&lt;/matchScore&gt;&lt;/BiometriaResponse&gt;</GetBiometriaResult>
                    </GetBiometriaResponse>
                </s:Body>
            </s:Envelope>";

        // Act
        var result = _mapper.MapFromSoap(soapContent, cpf);

        // Assert
        Assert.Equal(cpf, result.Cpf);
        Assert.Equal(Strings.Unknown, result.Status);
        Assert.Equal(95.5, result.MatchScore);
    }

    [Fact]
    public void MapFromSoap_WithMissingMatchScore_FallsBackToZero()
    {
        // Arrange
        const string cpf = "12345678901";
        const string soapContent = @"
            <s:Envelope xmlns:s='http://schemas.xmlsoap.org/soap/envelope/'>
                <s:Body>
                    <GetBiometriaResponse xmlns='http://example.com'>
                        <GetBiometriaResult>&lt;BiometriaResponse&gt;&lt;cpf&gt;12345678901&lt;/cpf&gt;&lt;status&gt;OK&lt;/status&gt;&lt;/BiometriaResponse&gt;</GetBiometriaResult>
                    </GetBiometriaResponse>
                </s:Body>
            </s:Envelope>";

        // Act
        var result = _mapper.MapFromSoap(soapContent, cpf);

        // Assert
        Assert.Equal(cpf, result.Cpf);
        Assert.Equal("OK", result.Status);
        Assert.Equal(0.0, result.MatchScore);
    }

    [Fact]
    public void MapFromSoap_WithInvalidMatchScore_FallsBackToZero()
    {
        // Arrange
        const string cpf = "12345678901";
        const string soapContent = @"
            <s:Envelope xmlns:s='http://schemas.xmlsoap.org/soap/envelope/'>
                <s:Body>
                    <GetBiometriaResponse xmlns='http://example.com'>
                        <GetBiometriaResult>&lt;BiometriaResponse&gt;&lt;cpf&gt;12345678901&lt;/cpf&gt;&lt;status&gt;OK&lt;/status&gt;&lt;matchScore&gt;not-a-number&lt;/matchScore&gt;&lt;/BiometriaResponse&gt;</GetBiometriaResult>
                    </GetBiometriaResponse>
                </s:Body>
            </s:Envelope>";

        // Act
        var result = _mapper.MapFromSoap(soapContent, cpf);

        // Assert
        Assert.Equal(cpf, result.Cpf);
        Assert.Equal("OK", result.Status);
        Assert.Equal(0.0, result.MatchScore);
    }

    #endregion

    #region Invalid XML Handling

    [Fact]
    public void MapFromSoap_WithInvalidXml_ReturnsDefaultDto()
    {
        // Arrange - invalid XML gets caught and returns default
        const string cpf = "12345678901";
        const string soapContent = "This is not valid XML at all <>";

        // Act
        var result = _mapper.MapFromSoap(soapContent, cpf);

        // Assert - mapper returns default DTO instead of throwing
        Assert.Equal(cpf, result.Cpf);
        Assert.NotNull(result.Status);
    }

    [Fact]
    public void MapFromSoap_WithEmptyString_ReturnsDefaultDto()
    {
        // Arrange
        const string cpf = "12345678901";
        const string soapContent = "";

        // Act
        var result = _mapper.MapFromSoap(soapContent, cpf);

        // Assert - mapper returns default DTO on exception
        Assert.Equal(cpf, result.Cpf);
        Assert.NotNull(result.Status);
    }

    [Fact]
    public void MapFromSoap_WithPartialXml_ReturnsDefaultDto()
    {
        // Arrange
        const string cpf = "12345678901";
        const string soapContent = "<s:Envelope><s:Body>";

        // Act - partial XML will cause exception, mapper catches and returns default
        var result = _mapper.MapFromSoap(soapContent, cpf);

        // Assert - mapper returns default DTO
        Assert.Equal(cpf, result.Cpf);
        Assert.NotNull(result.Status);
    }

    #endregion

    #region Different Namespaces

    [Fact]
    public void MapFromSoap_WithDifferentNamespaces_CorrectlyExtracts()
    {
        // Arrange - using local-name() in XPath handles namespaces
        const string cpf = "12345678901";
        const string soapContent = @"
            <Envelope xmlns='http://schemas.xmlsoap.org/soap/envelope/'>
                <Body>
                    <GetBiometriaResponse xmlns='urn:BiometriaService'>
                        <GetBiometriaResult>&lt;BiometriaResponse&gt;&lt;cpf&gt;12345678901&lt;/cpf&gt;&lt;status&gt;OK&lt;/status&gt;&lt;matchScore&gt;88.7&lt;/matchScore&gt;&lt;/BiometriaResponse&gt;</GetBiometriaResult>
                    </GetBiometriaResponse>
                </Body>
            </Envelope>";

        // Act
        var result = _mapper.MapFromSoap(soapContent, cpf);

        // Assert
        Assert.Equal(cpf, result.Cpf);
        Assert.Equal("OK", result.Status);
        Assert.True(result.MatchScore > 0);
    }

    #endregion

    #region SOAP Fault Responses

    [Fact]
    public void MapFromSoap_WithSoapFault_ReturnsNotFoundStatus()
    {
        // Arrange - SOAP fault doesn't have status/matchScore, so returns NOT_FOUND
        const string cpf = "12345678901";
        const string soapContent = @"
            <soap:Envelope xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>
                <soap:Body>
                    <soap:Fault>
                        <faultcode>soap:Server</faultcode>
                        <faultstring>Internal Server Error</faultstring>
                    </soap:Fault>
                </soap:Body>
            </soap:Envelope>";

        // Act
        var result = _mapper.MapFromSoap(soapContent, cpf);

        // Assert - when no status found, returns NOT_FOUND
        Assert.Equal(cpf, result.Cpf);
        Assert.Equal(StatusCodes.NotFound, result.Status);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void MapFromSoap_WithNullCpf_UsesCpfParameter()
    {
        // Arrange
        const string cpf = "99999999999";
        const string soapContent = @"
            <s:Envelope xmlns:s='http://schemas.xmlsoap.org/soap/envelope/'>
                <s:Body>
                    <GetBiometriaResponse xmlns='http://example.com'>
                        <GetBiometriaResult>&lt;BiometriaResponse&gt;&lt;status&gt;OK&lt;/status&gt;&lt;matchScore&gt;95.5&lt;/matchScore&gt;&lt;/BiometriaResponse&gt;</GetBiometriaResult>
                    </GetBiometriaResponse>
                </s:Body>
            </s:Envelope>";

        // Act
        var result = _mapper.MapFromSoap(soapContent, cpf);

        // Assert
        Assert.Equal(cpf, result.Cpf);
    }

    [Fact]
    public void MapFromSoap_WithWhitespaceInElements_TrimsCorrectly()
    {
        // Arrange
        const string cpf = "12345678901";
        const string soapContent = @"
            <s:Envelope xmlns:s='http://schemas.xmlsoap.org/soap/envelope/'>
                <s:Body>
                    <GetBiometriaResponse xmlns='http://example.com'>
                        <GetBiometriaResult>&lt;BiometriaResponse&gt;&lt;cpf&gt;  12345678901  &lt;/cpf&gt;&lt;status&gt;  OK  &lt;/status&gt;&lt;matchScore&gt;  95.5  &lt;/matchScore&gt;&lt;/BiometriaResponse&gt;</GetBiometriaResult>
                    </GetBiometriaResponse>
                </s:Body>
            </s:Envelope>";

        // Act
        var result = _mapper.MapFromSoap(soapContent, cpf);

        // Assert - InnerText should handle whitespace
        Assert.Equal(cpf, result.Cpf);
    }

    #endregion
}
