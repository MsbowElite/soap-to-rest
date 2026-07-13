using CsharpRest.Domain.Dto;

namespace CsharpRest.Application.Interfaces;

public interface IBiometriaMapper
{
    BiometriaDto MapFromSoap(string soapContent, string cpf);
}
