using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System.Text;

namespace DndOnePlaceManager.Application.Commands.Actions.BuildToken
{
    internal class BuildTokenCommandHandler : HandlerBase<BuildTokenCommand, BuiltToken>
    {
        // Same as the client's TokenManager: tokens on the token layer, their UI above it.
        private const int TokenLayer = 100;
        private const int TokenUiLayer = 110;

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
            var imageSource = "element";
            if (request.CardId is Guid cardId)
            {
                var card = await dbContext.Cards.Where(c => c.Id == cardId && c.GameId == request.GameId)
                    .Select(c => new { c.Name }).FirstOrDefaultAsync(cancellationToken)
                    ?? throw new ArgumentException($"No card with id '{cardId}' in this game.");
                var props = await dbContext.Properties.Where(p => p.ParentID == cardId && CardProps.Contains(p.Name))
                    .ToDictionaryAsync(p => p.Name!, p => p.Value, cancellationToken);
                tokenFile = props.GetValueOrDefault("token");
                if (string.IsNullOrWhiteSpace(tokenFile))
                    throw new ArgumentException($"The card '{card.Name}' has no \"token\" property naming its token file.");
                image = props.GetValueOrDefault("tokenImage");
                size = int.TryParse(props.GetValueOrDefault("drop_token_size"), out var s) && s > 0 ? s : 1;
                name = props.GetValueOrDefault("character_name")?.Trim() is { Length: > 0 } character ? character : card.Name;
                imageSource = "card";
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

            size ??= int.TryParse(template.Value<string>("size"), out var fileSize) && fileSize > 0 ? fileSize : 1;
            var grid = await dbContext.Maps.Where(m => m.Id == request.MapId && m.Game.Id == request.GameId)
                .Select(m => (int?)m.GridSize).FirstOrDefaultAsync(cancellationToken)
                ?? throw new ArgumentException($"No map with id '{request.MapId}' in this game.");
            var side = (grid > 0 ? grid : 50) * size.Value;

            var o = (JObject?)template["object"]?.DeepClone() ?? new JObject();
            o["name"] = name;
            o["left"] = request.X;
            o["top"] = request.Y;
            // The picture's own size isn't known here: the token is as wide as it should be at
            // scale 1, and the client fits the picture to that width when it loads it.
            o["width"] = side;
            o["height"] = side;
            o["scaleX"] = 1;
            o["scaleY"] = 1;
            o["cardId"] = request.CardId?.ToString();
            o["isToken"] = true;
            o["mapId"] = request.MapId.ToString();
            o["layer"] = TokenLayer;
            o["resourceId"] = image;

            var data = (JObject?)template["tokenData"]?.DeepClone() ?? new JObject();
            data["cardId"] = request.CardId?.ToString();
            data["editableProps"] = template["editableProps"]?.DeepClone() ?? new JArray();
            data["assignableIcons"] = template["assignableIcons"]?.DeepClone() ?? new JArray();
            var deps = (JArray?)data["propDeps"] ?? new JArray();
            deps.Add(new JObject { ["dtoProperty"] = "tokenImage", ["objectProperty"] = "src", ["type"] = "string", ["source"] = imageSource });
            data["propDeps"] = deps;
            o["tokenData"] = data;

            var ui = (JArray?)template["additions"]?.DeepClone() ?? new JArray();
            foreach (var element in ui.OfType<JObject>())
                element["layer"] = TokenUiLayer;
            o["tokenUiElements"] = ui;

            // A circle crop without a radius covers the token; the client fits it to the picture.
            if (o["clipPath"] is JObject clip && (string?)clip["type"] == "circle" && !((double?)clip["radius"] > 0))
                clip["radius"] = side / 2.0;

            return new BuiltToken(o, size.Value);
        }
    }
}
