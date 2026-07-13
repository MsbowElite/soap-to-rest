using CsharpRest.Domain.Dto;
using CsharpRest.Application.Models;
using System.Threading.Tasks;

namespace CsharpRest.Application.Interfaces;

public interface IBiometriaService
{
    Task<Result<BiometriaDto>> GetBiometriaAsync(string cpf);
}
