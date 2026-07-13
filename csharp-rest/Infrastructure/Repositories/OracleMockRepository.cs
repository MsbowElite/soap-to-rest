using CsharpRest.Application.Interfaces;
using CsharpRest.Domain.Dto;

namespace CsharpRest.Infrastructure.Repositories;

public class OracleMockRepository : IBiometriaRepository
{
    private readonly List<BiometriaDto> _store = new();
    private readonly ILoggerService _logger;

    public OracleMockRepository(ILoggerService logger)
    {
        _logger = logger;
    }

    public void Save(BiometriaDto record)
    {
        _store.Add(record);
        _logger.Information("[Repo] Saved record for {Cpf} (status={Status},score={Score})", record.Cpf, record.Status, record.MatchScore);
    }
}
