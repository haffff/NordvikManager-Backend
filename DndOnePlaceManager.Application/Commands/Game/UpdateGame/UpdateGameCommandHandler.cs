using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Game.UpdateGame
{
    public class UpdateGameCommandHandler : HandlerBase<UpdateGameCommand, CommandResponse>
    {
        public UpdateGameCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }
        public async override Task<CommandResponse> Handle(UpdateGameCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var game = dbContext.Games.FirstOrDefault(x => x.Id == request.GameId);
            if (game == null)
            {
                throw new ResourceNotFoundException(nameof(game));
            }

            game.ThrowIfNoPermission(request.Player.Id ?? Guid.Empty, Domain.Enums.Permission.Edit);

            game.Name = request?.Name ?? game.Name;
            game.Password = request.Password ?? request.Password;
            game.UseMetric = request.BaseDistanceUnit == "metric" ? true : false;

            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}
