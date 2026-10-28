using NordvikManager.Application.Mapping;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Game.UpdateGame
{
    public class UpdateGameCommandHandler : HandlerBase<UpdateGameCommand, CommandResponse>
    {
        public UpdateGameCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }
        public async override Task<CommandResponse> Handle(UpdateGameCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var game = dbContext.Games.Include(x => x.Properties).FirstOrDefault(x => x.Id == request.GameId);
            Guard.NotFound(game, "Game", request.GameId);

            if (!request.AsLocalAdmin)
                game.ThrowIfNoPermission(request.Player?.Id ?? Guid.Empty, Domain.Enums.Permission.Edit);

            // Only what was sent changes: the game list sends name and description, the in-game
            // settings name and password. An empty password clears it.
            game.Name = request.Name ?? game.Name;
            if (request.Password != null)
                game.Password = request.Password;
            if (request.BaseDistanceUnit != null)
                game.UseMetric = request.BaseDistanceUnit == "metric";

            if (request.ShortDescription != null)
            {
                game.Properties ??= new List<PropertyModel>();
                var description = game.Properties.FirstOrDefault(p => p.Name == "shortDescription");
                if (description == null)
                    game.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = "shortDescription", EntityName = nameof(GameModel), ParentID = game.Id, Value = request.ShortDescription });
                else
                    description.Value = request.ShortDescription;
            }

            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}
