using NordvikManager.Application.Mapping;
using NordvikManager.Application.Extension;
using NordvikManager.Domain.Entities.Chat;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Application.Commands.Chat.AddMessage
{
    internal class AddMessageCommandHandler : HandlerBase<AddMessageCommand, (CommandResponse, long)>
    {
        public AddMessageCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public async override Task<(CommandResponse, long)> Handle(AddMessageCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            MessageModel model = new MessageModel()
            {
                Content = request.Message,
                Created = DateTime.UtcNow,
                GameId = request.GameID,
                PlayerId = request.PlayerId,
                RecipientId = request.RecipientId,
                IsTemplate = request.IsTemplate
            };

            await dbContext.AddAsync(model);

            await dbContext.SaveChangesAsync();

            if (request.VisibleTo == null)
            {
                model.SetGlobalPermission();
            }
            else
            {
                foreach (var playerId in request.VisibleTo.Distinct())
                    model.SetPermissions(playerId, Permission.Read);
            }

            return (CommandResponse.Ok, 0);
        }
    }
}
