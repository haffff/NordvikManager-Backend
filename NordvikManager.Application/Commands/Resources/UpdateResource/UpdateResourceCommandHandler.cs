using NordvikManager.Application.Mapping;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using System.Linq;

namespace NordvikManager.Application.Commands.Resources.UpdateResource
{
    internal class UpdateResourceCommandHandler : HandlerBase<UpdateResourceCommand, CommandResponse>
    {
        private IMediator mediator;

        public UpdateResourceCommandHandler(IDbContext context, IMapper mapper, IMediator mediator) : base(context, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<CommandResponse> Handle(UpdateResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            Guard.Argument(request.Player != null, nameof(request.Player));

            // Bug fix: was scoping the lookup itself to `x.PlayerId == request.Player.Id`, so
            // anyone but the original uploader (e.g. the GM renaming a player-uploaded image)
            // got a silent NullReferenceException instead of a real permission error.
            var resourceModel = dbContext.Resources.FirstOrDefault(x => x.Id == request.Resource.Id);
            Guard.NotFound(resourceModel, "Resource", request.Resource.Id);

            // The uploader, or whoever may edit the game (owner or co-GM).
            var canEditGame = (request.Player.IsOwner ?? false) || ((request.Player.Permission ?? Permission.None) & Permission.Edit) != 0;
            if (resourceModel.PlayerId != request.Player.Id && !canEditGame)
                throw new PermissionException(Permission.Edit);

            if (request.Resource.Name != null)
                resourceModel.Name = request.Resource.Name;

            // Key is what addon actions/queries and getResourceString(key) use to look a
            // resource up without knowing its GUID — e.g. naming an image "Apple" so an
            // action can reference it directly instead of a %q:{guid}.prop% query. Unique
            // per game (same constraint AddResourceCommandHandler/SetResourceCommandHandler
            // already enforce for new resources), so a collision here is reported rather
            // than left to throw on SaveChanges.
            if (request.Resource.Key != resourceModel.Key)
            {
                var newKey = string.IsNullOrWhiteSpace(request.Resource.Key) ? null : request.Resource.Key.Trim();
                if (newKey != null)
                {
                    var keyTaken = dbContext.Resources.Any(x =>
                        x.Id != resourceModel.Id && x.GameId == resourceModel.GameId && x.Key == newKey);
                    if (keyTaken)
                        return CommandResponse.AlreadyExists;
                }
                resourceModel.Key = newKey;
            }

            if (request.Resource.Volume.HasValue)
                resourceModel.Volume = VolumeRules.Clamp(request.Resource.Volume.Value);

            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}
