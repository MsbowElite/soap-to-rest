using System.Text.RegularExpressions;

public static class SoapParser
{
    private static readonly Regex CpfRegex = new("<cpf>(\\d{11})</cpf>", RegexOptions.Compiled);
    private static readonly Regex StatusRegex = new("<status>([^<]+)</status>", RegexOptions.Compiled);

    public static (string cpf, string status) Parse(string soap)
    {
        if (string.IsNullOrEmpty(soap)) return (string.Empty, string.Empty);
        var cpfMatch = CpfRegex.Match(soap);
        var statusMatch = StatusRegex.Match(soap);
        var cpf = cpfMatch.Success ? cpfMatch.Groups[1].Value : string.Empty;
        var status = statusMatch.Success ? statusMatch.Groups[1].Value : string.Empty;
        return (cpf, status);
    }
}
