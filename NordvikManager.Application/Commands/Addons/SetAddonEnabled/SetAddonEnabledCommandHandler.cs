using NordvikManager.Application.Mapping;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Application.Commands.Addons.SetAddonEnabled
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
            Guard.NotFound(game, "Game", request.GameID);

            game.ThrowIfNoPermission(request.Player.Id ?? default, Permission.Edit);

            var addon = dbContext.Addons.FirstOrDefault(x =>
                x.Id.ToString() == request.AddonId || x.Key == request.AddonId);

            Guard.NotFound(addon, "Addon", request.AddonId);

            addon.IsEnabled = request.Enabled;
            await dbContext.SaveChangesAsync();

            return CommandResponse.Ok;
        }
    }
}
