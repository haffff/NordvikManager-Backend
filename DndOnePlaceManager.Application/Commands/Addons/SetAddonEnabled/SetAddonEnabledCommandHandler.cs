using AutoMapper;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Addons.SetAddonEnabled
{
    internal class SetAddonEnabledCommandHandler : HandlerBase<SetAddonEnabledCommand, CommandResponse>
    {
        public SetAddonEnabledCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(SetAddonEnabledCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var game = dbContext.Games.FirstOrDefault(x => x.Id == request.GameID);
            if (game == null)
                return CommandResponse.NoResource;

            if (!game.HasPermission(request.Player.Id ?? default, Permission.Edit))
                return CommandResponse.NoPermission;

            var addon = dbContext.Addons.FirstOrDefault(x =>
                x.Id.ToString() == request.AddonId || x.Key == request.AddonId);

            if (addon == null)
                return CommandResponse.NoResource;

            addon.IsEnabled = request.Enabled;
            await dbContext.SaveChangesAsync();

            return CommandResponse.Ok;
        }
    }
}
