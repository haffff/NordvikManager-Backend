using NordvikManager.Application.Commands.Resources;
using NordvikManager.Application.Extension;
using NordvikManager.Domain.Enums;
using NordvikManager.Application.Mapping;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System.Text;

namespace NordvikManager.Application.Commands.Actions.BuildToken
{
    internal class BuildTokenCommandHandler : HandlerBase<BuildTokenCommand, BuiltToken>
    {
        private static readonly string[] CardProps = { "token", "tokenImage", "drop_token_size", "character_name" };

        private readonly IMediator mediator;

        public BuildTokenCommandHandler(IDbContext dbContext, IMapper mapper, IMediator mediator) : base(dbContext, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<BuiltToken> Handle(BuildTokenCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            string? tokenFile = request.Token, image = request.Image, name = "Token";
            int? size = null;
            if (request.CardId is Guid cardId)
            {
                var card = await dbContext.Cards.FirstOrDefaultAsync(c => c.Id == cardId && c.GameId == request.GameId, cancellationToken)
                    ?? throw new ArgumentException($"No card with id '{cardId}' in this game.");
                // Its token file, picture and name are the card's: only for who may read it.
                card.ThrowIfNoPermission(request.Player.Id ?? default, Permission.Read);
                var props = await dbContext.Properties.Where(p => p.ParentID == cardId && CardProps.Contains(p.Name))
                    .ToDictionaryAsync(p => p.Name!, p => p.Value, cancellationToken);
                tokenFile = props.GetValueOrDefault("token");
                if (string.IsNullOrWhiteSpace(tokenFile))
                    throw new ArgumentException($"The card '{card.Name}' has no \"token\" property naming its token file.");
                image = props.GetValueOrDefault("tokenImage");
                size = int.TryParse(props.GetValueOrDefault("drop_token_size"), out var s) && s > 0 ? s : null;
                name = props.GetValueOrDefault("character_name")?.Trim() is { Length: > 0 } character ? character : card.Name;
            }
            if (string.IsNullOrWhiteSpace(tokenFile))
                throw new ArgumentException("A token needs a card or a token file.");

            var (bytes, _) = await mediator.Send(new GetResourceDataCommand
            {
                GameID = request.GameId,
                Player = request.Player,
                Key = Guid.TryParse(tokenFile, out _) ? null : tokenFile,
                ID = Guid.TryParse(tokenFile, out var tokenId) ? tokenId : null,
            }, cancellationToken);
            if (bytes == null)
                throw new ArgumentException($"No token file '{tokenFile}' in this game.");
            var template = JObject.Parse(Encoding.UTF8.GetString(bytes));

            var grid = await dbContext.Maps.Where(m => m.Id == request.MapId && m.Game.Id == request.GameId)
                .Select(m => (int?)m.GridSize).FirstOrDefaultAsync(cancellationToken)
                ?? throw new ArgumentException($"No map with id '{request.MapId}' in this game.");

            return TokenBuild.Build(template, new TokenPlacement(
                grid, request.X, request.Y, request.MapId.ToString(), image, request.CardId?.ToString(), name, size));
        }
    }
}
