using AutoMapper;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Entities.Chat;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Chat.AddMessage
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
                PlayerId = request.PlayerId
            };

            await dbContext.AddAsync(model);

            await dbContext.SaveChangesAsync();

            model.SetGlobalPermission();

            return (CommandResponse.Ok, 0);
        }
    }
}
