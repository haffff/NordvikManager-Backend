using AutoMapper;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    public class UpdateBattleMapCommandHandler : HandlerBase<UpdateBattleMapCommand, CommandResponse>
    {
        public UpdateBattleMapCommandHandler(IDbContext context, IMapper mapper) : base(context, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(UpdateBattleMapCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            if (request.Player == null)
            {
                return CommandResponse.WrongArguments;
            }

            var model = dbContext.BattleMaps.FirstOrDefault(x=>x.Id == request.Dto.Id);

            model.ThrowIfNoPermission(request.Player.Id ?? Guid.Empty, Domain.Enums.Permission.Edit);

            model.Name = request.Dto.Name ?? model.Name;
            model.MapId = request.Dto.MapId ?? model.MapId;

            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}

