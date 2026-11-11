using MediatR;
using NordvikManager.Application.Commands.Game.GetGameCentralSessionId;
using NordvikManager.Application.Commands.Game.GetGameSessionDetails;
using NordvikManager.Application.Commands.Game.SetGameCentralSession;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Services
{
    /// <summary>
    /// The game's Central session and the details it was made from. <see cref="Details"/> is
    /// null when the game is gone; <see cref="SessionId"/> is null when Central has none for it
    /// and making one failed, or (<see cref="NeedsOwner"/>) wasn't this user's to make.
    /// </summary>
    public sealed record CentralSessionResult(string? SessionId, GetGameSessionDetailsResult? Details, bool NeedsOwner = false);

    /// <summary>
    /// Keeps a game's Central session in step with the game: made when the game has none, its
    /// listing (cover image, password) re-sent on every start, and made again when Central no
    /// longer has it (expired after its GM stayed away, or deleted).
    /// </summary>
    public sealed class CentralSessionSync
    {
        private readonly IMediator _mediator;
        private readonly ICentralServerService _central;

        public CentralSessionSync(IMediator mediator, ICentralServerService central)
        {
            _mediator = mediator;
            _central = central;
        }

        /// <param name="mayCreate">
        /// Whether this user may make the session when one is needed. Whoever makes it owns it on
        /// Central, and the owner may connect to signaling as the game's GM, so only the game's GM
        /// (or the server's local admin) may.
        /// </param>
        public async Task<CentralSessionResult> EnsureAsync(Guid gameId, string centralToken, bool mayCreate)
        {
            var details = await _mediator.Send(new GetGameSessionDetailsCommand { GameId = gameId });
            if (details == null)
                return new CentralSessionResult(null, null);

            var sessionId = await _mediator.Send(new GetGameCentralSessionIdCommand { GameID = gameId });
            if (sessionId != null)
            {
                // Best effort: unreachable, refused or a Central too old for the route leaves
                // the listing as it was. Only a session Central no longer has is made again.
                var update = await _central.UpdateSessionAsync(centralToken, sessionId, ToItem(details));
                if (update != CentralSessionUpdate.Missing)
                    return new CentralSessionResult(sessionId, details);
            }

            if (!mayCreate)
                return new CentralSessionResult(null, details, NeedsOwner: true);
            return new CentralSessionResult(await CreateAsync(gameId, centralToken, details), details);
        }

        /// <summary>A new Central session for the game; the one it replaces is deleted once the new one exists.</summary>
        public async Task<CentralSessionResult> ReplaceAsync(Guid gameId, string centralToken)
        {
            var details = await _mediator.Send(new GetGameSessionDetailsCommand { GameId = gameId });
            if (details == null)
                return new CentralSessionResult(null, null);

            var oldId = await _mediator.Send(new GetGameCentralSessionIdCommand { GameID = gameId });
            var newId = await CreateAsync(gameId, centralToken, details);
            if (newId != null && oldId != null && oldId != newId)
                await _central.DeleteSessionAsync(centralToken, oldId);
            return new CentralSessionResult(newId, details);
        }

        private async Task<string?> CreateAsync(Guid gameId, string centralToken, GetGameSessionDetailsResult details)
        {
            var sessionId = await _central.CreateSessionAsync(centralToken, ToItem(details));
            if (sessionId != null)
                await _mediator.Send(new SetGameCentralSessionCommand { GameId = gameId, CentralSessionId = sessionId });
            return sessionId;
        }

        private static GameItemDTO ToItem(GetGameSessionDetailsResult details) => new()
        {
            Name = details.Name,
            ShortDescription = details.Summary,
            LongDescription = details.Description,
            Image = details.Image,
            PasswordRequired = details.PasswordRequired,
            Password = details.Password,
            IsPublic = details.IsPublic,
        };
    }
}
