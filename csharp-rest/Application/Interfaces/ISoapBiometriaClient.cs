using System.Net.Http;
using System.Threading.Tasks;

namespace CsharpRest.Application.Interfaces;

public interface ISoapBiometriaClient
{
    Task<HttpResponseMessage> SendBiometriaRequestAsync(string cpf);
}
