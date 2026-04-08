using DNDOnePlaceManager.Domain.Entities.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services
{
    public class WebSocketTokenValidator : IWebSocketTokenValidator
    {
        private readonly IConfiguration _configuration;

        public WebSocketTokenValidator(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<User?> ValidateTokenAsync(string token)
        {
            var jwtSecret = _configuration["JWTSecret"];
            if (string.IsNullOrEmpty(jwtSecret))
                return Task.FromResult<User?>(null);

            var tokenHandler = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                NameClaimType = "username",
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                var principal = tokenHandler.ValidateToken(token, parameters, out _);

                var user = new User
                {
                    Id = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                         ?? principal.FindFirst("sub")?.Value
                         ?? string.Empty,
                    UserName = principal.Identity?.Name,
                    Email = principal.FindFirst("email")?.Value,
                    IsAdmin = principal.FindFirst("isAdmin")?.Value?.ToLowerInvariant() == "true"
                };

                return Task.FromResult<User?>(user);
            }
            catch (SecurityTokenException)
            {
                return Task.FromResult<User?>(null);
            }
        }
    }
}
