using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Chat;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Chat.GetMessages
{
    internal class GetMessagesCommandHandler : HandlerBase<GetMessagesCommand, List<MessageDTO>>
    {

        public GetMessagesCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public async override Task<List<MessageDTO>> Handle(GetMessagesCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var query = dbContext.Messages.WithPermission(request.PlayerID)
                .Where(x => x.GameId == request.GameID);

            if (!string.IsNullOrWhiteSpace(request.Filter))
            {
                query = query.Where(x => x.Content.Contains(request.Filter));
            }

            if (request.From != default)
            {
                query = query.Where(x => x.PlayerId == request.From);
            }

            var messages = query.OrderByDescending(x => x.Created)
                .Skip(request.Page * request.Size)
                .Take(request.Size);

            return mapper.Map<List<MessageDTO>>(messages);
        }
    }
}
