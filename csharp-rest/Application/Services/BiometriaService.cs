using CsharpRest.Application.Constants;
using CsharpRest.Application.Interfaces;
using CsharpRest.Application.Models;
using CsharpRest.Domain.Dto;
using CsharpRest.Infrastructure.Repositories;

namespace CsharpRest.Application.Services;

public class BiometriaService : IBiometriaService
{
    private readonly ISoapBiometriaClient _soapClient;
    private readonly IBiometriaMapper _mapper;
    private readonly IBiometriaRepository _repo;
    private readonly ILoggerService _logger;

    public BiometriaService(ISoapBiometriaClient soapClient, IBiometriaMapper mapper, IBiometriaRepository repo, ILoggerService logger)
    {
        _soapClient = soapClient;
        _mapper = mapper;
        _repo = repo;
        _logger = logger;
    }

    public async Task<Result<BiometriaDto>> GetBiometriaAsync(string cpf)
    {
        try
        {
            var response = await _soapClient.SendBiometriaRequestAsync(cpf);

            if (!response.IsSuccessStatusCode)
            {
                var msg = $"SOAP upstream returned {(int)response.StatusCode} {response.ReasonPhrase}";
                _logger.Error(null, "[Service] {Msg} for {Cpf}", msg, cpf);
                return Result<BiometriaDto>.Failure(msg);
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.Information("[Service] SOAP response length {Len} for {Cpf}", content?.Length ?? 0, cpf);

            var dto = _mapper.MapFromSoap(content ?? string.Empty, cpf);
            if (dto.Status == CsharpRest.Application.Constants.StatusCodes.NotFound)
            {
                _logger.Information("[Service] Biometria not found for {Cpf}", cpf);
                return Result<BiometriaDto>.NotFound(Strings.BiometriaNotFound);
            }

            _repo.Save(dto);
            return Result<BiometriaDto>.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[Service] Unhandled error while fetching biometria for {Cpf}", cpf);
            return Result<BiometriaDto>.Failure(ex.Message);
        }
    }
}
