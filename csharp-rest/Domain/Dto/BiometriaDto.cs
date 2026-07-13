namespace CsharpRest.Domain.Dto;

public sealed class BiometriaDto
{
    public string Cpf { get; }
    public string Status { get; }
    public double MatchScore { get; }

    // Constructor-based mapping: keeps working if a caller omits a parameter
    public BiometriaDto(string cpf, string? status = null, double? matchScore = null)
    {
        Cpf = cpf ?? string.Empty;
        Status = status ?? CsharpRest.Application.Constants.Strings.Unknown;
        MatchScore = matchScore ?? 0.0;
    }
}
