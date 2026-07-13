using System;
using System.Net;
using System.Net.Http;
using System.Xml;
using Xunit;
using Moq;
using CsharpRest.Application.Services;
using CsharpRest.Application.Interfaces;
using CsharpRest.Domain.Dto;
using CsharpRest.Infrastructure.Mappers;
using CsharpRest.Infrastructure.Repositories;

namespace CsharpRest.Tests;

public class BiometriaServiceTests
{
    private readonly Mock<ISoapBiometriaClient> _mockSoapClient;
    private readonly Mock<IBiometriaMapper> _mockMapper;
    private readonly Mock<IBiometriaRepository> _mockRepository;
    private readonly Mock<ILoggerService> _mockLogger;
    private readonly BiometriaService _service;

    public BiometriaServiceTests()
    {
        _mockSoapClient = new Mock<ISoapBiometriaClient>();
        _mockMapper = new Mock<IBiometriaMapper>();
        _mockRepository = new Mock<IBiometriaRepository>();
        _mockLogger = new Mock<ILoggerService>();

        _service = new BiometriaService(_mockSoapClient.Object, _mockMapper.Object, _mockRepository.Object, _mockLogger.Object);
    }

    #region Success Scenarios

    [Fact]
    public async Task GetBiometriaAsync_WithValidResponse_ReturnsSuccessResult()
    {
        // Arrange
        const string cpf = "12345678901";
        var soapResponse = @"<soap>...</soap>";
        var dto = new BiometriaDto(cpf, "OK", 95.5);

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(soapResponse)
        };

        _mockSoapClient
            .Setup(x => x.SendBiometriaRequestAsync(cpf))
            .ReturnsAsync(httpResponse);

        _mockMapper
            .Setup(x => x.MapFromSoap(soapResponse, cpf))
            .Returns(dto);

        // Act
        var result = await _service.GetBiometriaAsync(cpf);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsNotFound);
        Assert.NotNull(result.Value);
        Assert.Equal(cpf, result.Value!.Cpf);
        Assert.Equal("OK", result.Value.Status);
        Assert.Equal(95.5, result.Value.MatchScore);

        _mockSoapClient.Verify(x => x.SendBiometriaRequestAsync(cpf), Times.Once);
        _mockMapper.Verify(x => x.MapFromSoap(soapResponse, cpf), Times.Once);
        _mockRepository.Verify(x => x.Save(dto), Times.Once);
    }

    [Fact]
    public async Task GetBiometriaAsync_WithDifferentScores_ReturnsCorrectScores()
    {
        // Arrange
        var testScores = new[] { 75.3, 100.0, 0.5, 50.0 };

        foreach (var score in testScores)
        {
            const string cpf = "12345678901";
            var soapResponse = @"<soap>...</soap>";
            var dto = new BiometriaDto(cpf, "OK", score);

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(soapResponse)
            };

            _mockSoapClient
                .Setup(x => x.SendBiometriaRequestAsync(cpf))
                .ReturnsAsync(httpResponse);

            _mockMapper
                .Setup(x => x.MapFromSoap(soapResponse, cpf))
                .Returns(dto);

            // Act
            var result = await _service.GetBiometriaAsync(cpf);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(score, result.Value!.MatchScore);

            _mockSoapClient.Reset();
            _mockMapper.Reset();
            _mockRepository.Reset();
        }
    }

    #endregion

    #region Not Found Scenarios

    [Fact]
    public async Task GetBiometriaAsync_WhenStatusIsNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        const string cpf = "12345678901";
        var soapResponse = @"<soap>...</soap>";
        var dto = new BiometriaDto(cpf, "NOT_FOUND", 0.0);

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(soapResponse)
        };

        _mockSoapClient
            .Setup(x => x.SendBiometriaRequestAsync(cpf))
            .ReturnsAsync(httpResponse);

        _mockMapper
            .Setup(x => x.MapFromSoap(soapResponse, cpf))
            .Returns(dto);

        // Act
        var result = await _service.GetBiometriaAsync(cpf);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsNotFound);
        Assert.Null(result.Value);

        _mockSoapClient.Verify(x => x.SendBiometriaRequestAsync(cpf), Times.Once);
        _mockMapper.Verify(x => x.MapFromSoap(soapResponse, cpf), Times.Once);
        _mockRepository.Verify(x => x.Save(It.IsAny<BiometriaDto>()), Times.Never);
    }

    #endregion

    #region SOAP Client Error Scenarios

    [Fact]
    public async Task GetBiometriaAsync_WhenSoapReturnsError_ReturnsFailureResult()
    {
        // Arrange
        const string cpf = "12345678901";
        var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            ReasonPhrase = "Bad Request"
        };

        _mockSoapClient
            .Setup(x => x.SendBiometriaRequestAsync(cpf))
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.GetBiometriaAsync(cpf);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(result.IsNotFound);
        Assert.Null(result.Value);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("400", result.ErrorMessage);

        _mockMapper.Verify(x => x.MapFromSoap(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockRepository.Verify(x => x.Save(It.IsAny<BiometriaDto>()), Times.Never);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GetBiometriaAsync_WithVariousHttpErrors_ReturnsFailure(HttpStatusCode statusCode)
    {
        // Arrange
        const string cpf = "12345678901";
        var httpResponse = new HttpResponseMessage(statusCode);

        _mockSoapClient
            .Setup(x => x.SendBiometriaRequestAsync(cpf))
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.GetBiometriaAsync(cpf);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task GetBiometriaAsync_WhenSoapThrowsException_ReturnsFailureResult()
    {
        // Arrange
        const string cpf = "12345678901";
        var exception = new HttpRequestException("Network error");

        _mockSoapClient
            .Setup(x => x.SendBiometriaRequestAsync(cpf))
            .ThrowsAsync(exception);

        // Act
        var result = await _service.GetBiometriaAsync(cpf);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Network error", result.ErrorMessage);

        _mockMapper.Verify(x => x.MapFromSoap(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockRepository.Verify(x => x.Save(It.IsAny<BiometriaDto>()), Times.Never);
    }

    [Fact]
    public async Task GetBiometriaAsync_WhenMapperThrowsException_ReturnsFailureResult()
    {
        // Arrange
        const string cpf = "12345678901";
        var soapResponse = "invalid xml";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(soapResponse)
        };

        _mockSoapClient
            .Setup(x => x.SendBiometriaRequestAsync(cpf))
            .ReturnsAsync(httpResponse);

        _mockMapper
            .Setup(x => x.MapFromSoap(soapResponse, cpf))
            .Throws(new XmlException("Invalid XML"));

        // Act
        var result = await _service.GetBiometriaAsync(cpf);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Invalid XML", result.ErrorMessage);

        _mockRepository.Verify(x => x.Save(It.IsAny<BiometriaDto>()), Times.Never);
    }

    #endregion

    #region Empty/Null Response Scenarios

    [Fact]
    public async Task GetBiometriaAsync_WhenResponseContentIsNull_ReturnsFailureResult()
    {
        // Arrange
        const string cpf = "12345678901";
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("")
        };

        _mockSoapClient
            .Setup(x => x.SendBiometriaRequestAsync(cpf))
            .ReturnsAsync(httpResponse);

        _mockMapper
            .Setup(x => x.MapFromSoap("", cpf))
            .Throws(new Exception("Empty response"));

        // Act
        var result = await _service.GetBiometriaAsync(cpf);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    #endregion

    #region Repository Interaction Tests

    [Fact]
    public async Task GetBiometriaAsync_OnSuccess_SavesToRepository()
    {
        // Arrange
        const string cpf = "12345678901";
        var soapResponse = @"<soap>...</soap>";
        var dto = new BiometriaDto(cpf, "OK", 95.5);

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(soapResponse)
        };

        _mockSoapClient
            .Setup(x => x.SendBiometriaRequestAsync(cpf))
            .ReturnsAsync(httpResponse);

        _mockMapper
            .Setup(x => x.MapFromSoap(soapResponse, cpf))
            .Returns(dto);

        // Act
        await _service.GetBiometriaAsync(cpf);

        // Assert
        _mockRepository.Verify(x => x.Save(It.Is<BiometriaDto>(d => d.Cpf == cpf && d.Status == "OK")), Times.Once);
    }

    [Fact]
    public async Task GetBiometriaAsync_OnNotFound_DoesNotSaveToRepository()
    {
        // Arrange
        const string cpf = "12345678901";
        var soapResponse = @"<soap>...</soap>";
        var dto = new BiometriaDto(cpf, "NOT_FOUND", 0.0);

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(soapResponse)
        };

        _mockSoapClient
            .Setup(x => x.SendBiometriaRequestAsync(cpf))
            .ReturnsAsync(httpResponse);

        _mockMapper
            .Setup(x => x.MapFromSoap(soapResponse, cpf))
            .Returns(dto);

        // Act
        await _service.GetBiometriaAsync(cpf);

        // Assert
        _mockRepository.Verify(x => x.Save(It.IsAny<BiometriaDto>()), Times.Never);
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task GetBiometriaAsync_OnSuccess_LogsInformation()
    {
        // Arrange
        const string cpf = "12345678901";
        var soapResponse = @"<soap>...</soap>";
        var dto = new BiometriaDto(cpf, "OK", 95.5);

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(soapResponse)
        };

        _mockSoapClient
            .Setup(x => x.SendBiometriaRequestAsync(cpf))
            .ReturnsAsync(httpResponse);

        _mockMapper
            .Setup(x => x.MapFromSoap(soapResponse, cpf))
            .Returns(dto);

        // Act
        await _service.GetBiometriaAsync(cpf);

        // Assert
        _mockLogger.Verify(
            x => x.Information(It.IsAny<string>(), It.IsAny<object[]>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetBiometriaAsync_OnError_LogsError()
    {
        // Arrange
        const string cpf = "12345678901";
        var exception = new HttpRequestException("Network error");

        _mockSoapClient
            .Setup(x => x.SendBiometriaRequestAsync(cpf))
            .ThrowsAsync(exception);

        // Act
        await _service.GetBiometriaAsync(cpf);

        // Assert
        _mockLogger.Verify(
            x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Result Type Tests

    [Fact]
    public async Task GetBiometriaAsync_ReturnsResultType_WithAllProperties()
    {
        // Arrange
        const string cpf = "12345678901";
        var soapResponse = @"<soap>...</soap>";
        var dto = new BiometriaDto(cpf, "OK", 95.5);

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(soapResponse)
        };

        _mockSoapClient
            .Setup(x => x.SendBiometriaRequestAsync(cpf))
            .ReturnsAsync(httpResponse);

        _mockMapper
            .Setup(x => x.MapFromSoap(soapResponse, cpf))
            .Returns(dto);

        // Act
        var result = await _service.GetBiometriaAsync(cpf);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess || result.IsNotFound);
        Assert.NotNull(result.Value);
    }

    #endregion
}

public class BiometriaServiceIntegrationTests
{
    #region Integration with real mapper

    [Fact]
    public async Task GetBiometriaAsync_WithRealMapper_CorrectlyProcessesSoapResponse()
    {
        // Arrange
        var mockSoapClient = new Mock<ISoapBiometriaClient>();
        var mockRepository = new Mock<IBiometriaRepository>();
        var mockLogger = new Mock<ILoggerService>();
        var realMapper = new CsharpRest.Infrastructure.Mappers.BiometriaMapper();

        var service = new BiometriaService(mockSoapClient.Object, realMapper, mockRepository.Object, mockLogger.Object);

        const string cpf = "12345678901";
        var soapResponse = @"
            <s:Envelope xmlns:s='http://schemas.xmlsoap.org/soap/envelope/'>
                <s:Body>
                    <GetBiometriaResponse xmlns='http://example.com'>
                        <GetBiometriaResult>&lt;BiometriaResponse&gt;&lt;cpf&gt;12345678901&lt;/cpf&gt;&lt;status&gt;OK&lt;/status&gt;&lt;matchScore&gt;95.5&lt;/matchScore&gt;&lt;/BiometriaResponse&gt;</GetBiometriaResult>
                    </GetBiometriaResponse>
                </s:Body>
            </s:Envelope>";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(soapResponse)
        };

        mockSoapClient
            .Setup(x => x.SendBiometriaRequestAsync(cpf))
            .ReturnsAsync(httpResponse);

        // Act
        var result = await service.GetBiometriaAsync(cpf);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(cpf, result.Value!.Cpf);
        Assert.Equal("OK", result.Value.Status);
        Assert.Equal(95.5, result.Value.MatchScore);

        mockRepository.Verify(x => x.Save(It.IsAny<BiometriaDto>()), Times.Once);
    }

    #endregion
}
