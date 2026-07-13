using System;
using Xunit;
using FluentValidation.TestHelper;
using CsharpRest.Application.Models;
using CsharpRest.Application.Validators;

namespace CsharpRest.Tests;

public class BiometriaRequestValidatorTests
{
    private readonly BiometriaRequestValidator _validator = new();

    #region Valid CPF Tests

    [Fact]
    public void Validate_WithValidCpf_Succeeds()
    {
        // Arrange - using a mathematically valid CPF
        var request = new BiometriaRequest { Cpf = "12345678909" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Invalid CPF Tests - Format

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_WithNullOrEmptyCpf_Fails(string? cpf)
    {
        // Arrange
        var request = new BiometriaRequest { Cpf = cpf };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Cpf);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456")]
    [InlineData("12345678901234")]
    [InlineData("1234567890")]
    public void Validate_WithWrongNumberOfDigits_Fails(string cpf)
    {
        // Arrange
        var request = new BiometriaRequest { Cpf = cpf };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Cpf);
    }

    [Theory]
    [InlineData("123.456.789-01")] // Formatted CPF
    [InlineData("123-456-789-01")] // Wrong format
    [InlineData("12345678901a")]   // Contains letter
    [InlineData("1234567890!")]    // Contains special character
    [InlineData("abcdefghijk")]    // All letters
    public void Validate_WithNonNumericCpf_Fails(string cpf)
    {
        // Arrange
        var request = new BiometriaRequest { Cpf = cpf };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Cpf);
    }

    #endregion

    #region Invalid CPF Tests - Algorithm

    [Theory]
    [InlineData("00000000000")] // All zeros
    [InlineData("11111111111")] // All ones
    [InlineData("22222222222")] // All twos
    [InlineData("33333333333")] // All threes
    [InlineData("44444444444")] // All fours
    [InlineData("55555555555")] // All fives
    [InlineData("66666666666")] // All sixes
    [InlineData("77777777777")] // All sevens
    [InlineData("88888888888")] // All eights
    [InlineData("99999999999")] // All nines
    public void Validate_WithAllEqualDigits_Fails(string cpf)
    {
        // Arrange
        var request = new BiometriaRequest { Cpf = cpf };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Cpf);
    }

    [Theory]
    [InlineData("12345678902")] // Invalid first check digit
    [InlineData("12345678912")] // Invalid second check digit
    [InlineData("12345678911")] // Both check digits wrong
    public void Validate_WithInvalidCheckDigits_Fails(string cpf)
    {
        // Arrange
        var request = new BiometriaRequest { Cpf = cpf };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Cpf);
    }

    #endregion
}
