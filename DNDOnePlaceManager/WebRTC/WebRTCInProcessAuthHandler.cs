using DndOnePlaceManager.Application.DataTransferObjects.Game;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebRTC
{
    public class WebRTCInProcessAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public WebRTCInProcessAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Context.Items.TryGetValue("WebRTCPlayer", out var obj) || obj is not PlayerDTO player)
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, player.CentralServerUserId ?? string.Empty),
                new Claim("username", player.Name ?? string.Empty),
                new Claim("email", string.Empty),
                new Claim("isAdmin", "false"),
            };

            var identity  = new ClaimsIdentity(claims, Scheme.Name, "username", ClaimTypes.Role);
            var principal = new ClaimsPrincipal(identity);
            var ticket    = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
