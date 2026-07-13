using System.Xml;
using System.Net;
using CsharpRest.Application.Constants;
using CsharpRest.Application.Interfaces;
using CsharpRest.Domain.Dto;

namespace CsharpRest.Infrastructure.Mappers;

public class BiometriaMapper : IBiometriaMapper
{
    // No external frameworks; constructor-based mapping ensures resilience
    public BiometriaDto MapFromSoap(string soapContent, string cpf)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(soapContent);

            // First: try to find inner escaped XML in <GetBiometriaResult>
            var resultNode = doc.SelectSingleNode("//*[local-name()='GetBiometriaResult']");
            string? status = null;
            double? score = null;

            if (resultNode != null)
            {
                // the SOAP result may contain escaped XML as inner text (e.g. &lt;BiometriaResponse&gt;...)
                var inner = WebUtility.HtmlDecode(resultNode.InnerText ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(inner))
                {
                    try
                    {
                        var innerDoc = new XmlDocument();
                        innerDoc.LoadXml(inner);
                        var sNode = innerDoc.SelectSingleNode("//*[local-name()='status']");
                        if (sNode != null)
                            status = sNode.InnerText;

                        var scNode = innerDoc.SelectSingleNode("//*[local-name()='matchScore']");
                        if (scNode != null && double.TryParse(scNode.InnerText, out var parsed))
                            score = parsed;
                    }
                    catch (XmlException)
                    {
                        // fall back to top-level parsing below
                    }
                }
            }

            // If we didn't get values from inner result, try to find directly in the SOAP body
            if (status == null && score == null)
            {
                var statusNode = doc.SelectSingleNode("//*[local-name()='status']");
                if (statusNode != null)
                    status = statusNode.InnerText;

                var scoreNode = doc.SelectSingleNode("//*[local-name()='matchScore']");
                if (scoreNode != null && double.TryParse(scoreNode.InnerText, out var parsed2))
                    score = parsed2;
            }

            // If still nothing meaningful found, treat as NOT_FOUND
            if (status == null && score == null)
            {
                return new BiometriaDto(cpf, CsharpRest.Application.Constants.StatusCodes.NotFound, 0);
            }

            return new BiometriaDto(cpf, status, score);
        }
        catch (XmlException)
        {
            // high-density logging happens in service; return a safe default
            return new BiometriaDto(cpf);
        }
    }
}
