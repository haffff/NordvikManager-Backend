using NordvikManager.Domain.Entities.Auth;
using System.Threading.Tasks;

namespace NordvikManager.Services
{
    public interface IWebSocketTokenValidator
    {
        Task<User?> ValidateTokenAsync(string token);
    }
}
