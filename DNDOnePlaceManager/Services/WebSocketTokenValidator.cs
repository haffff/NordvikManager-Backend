using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using DNDOnePlaceManager.Domain.Entities.Auth;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services
{
    public class WebSocketTokenValidator : IWebSocketTokenValidator
    {
        public IConfiguration configuration;
        private readonly UserManager<User> userManager;

        public WebSocketTokenValidator(IConfiguration configuration, UserManager<User> userManager)
        {
            this.configuration = configuration;
            this.userManager = userManager;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<User> ValidateTokenAsync(string token)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            JwtSecurityToken jwtToken = tokenHandler.ReadJwtToken(token);
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidAudience = configuration["JWT:ValidAudience"],
                ValidIssuer = configuration["JWT:ValidIssuer"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:Secret"])),
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                var principal = tokenHandler.ValidateToken(token, parameters, out var validatedToken);
                var user = await userManager.FindByNameAsync(principal.Identity.Name);
                return user;
            }
            catch (SecurityTokenException exception)
            {
                return null;
            }
        }
    }
}
