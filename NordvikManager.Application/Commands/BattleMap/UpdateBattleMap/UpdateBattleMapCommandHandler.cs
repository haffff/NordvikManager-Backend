using NordvikManager.Application.Mapping;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.BattleMap
{
    public class UpdateBattleMapCommandHandler : HandlerBase<UpdateBattleMapCommand, CommandResponse>
    {
        public UpdateBattleMapCommandHandler(IDbContext context, IMapper mapper) : base(context, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(UpdateBattleMapCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            Guard.Argument(request.Player != null, nameof(request.Player));

            var model = dbContext.BattleMaps.FirstOrDefault(x => x.Id == request.Dto.Id);
            Guard.NotFound(model, "BattleMap", request.Dto.Id);

            model.ThrowIfNoPermission(request.Player.Id ?? Guid.Empty, Domain.Enums.Permission.Edit);

            model.Name = request.Dto.Name ?? model.Name;
            model.MapId = request.Dto.MapId ?? model.MapId;

            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}

