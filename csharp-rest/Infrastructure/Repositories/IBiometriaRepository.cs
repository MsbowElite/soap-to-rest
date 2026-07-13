using CsharpRest.Domain.Dto;

namespace CsharpRest.Infrastructure.Repositories;

public interface IBiometriaRepository
{
    void Save(BiometriaDto record);
}
