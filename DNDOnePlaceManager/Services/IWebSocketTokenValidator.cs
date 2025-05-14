using DNDOnePlaceManager.Domain.Entities.Auth;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services
{
    public interface IWebSocketTokenValidator
    {
        Task<User> ValidateTokenAsync(string token);
    }
}