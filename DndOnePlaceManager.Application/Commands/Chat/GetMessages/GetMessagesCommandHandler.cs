using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Chat;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
            // Everything below runs in SQL; permissions are checked before paging so hidden
            // messages don't take up page slots.
            var query = dbContext.Messages.AsNoTracking()
                .Where(x => x.GameId == request.GameID);

            if (!string.IsNullOrWhiteSpace(request.Filter))
            {
                query = query.Where(x => x.Content.Contains(request.Filter));
            }

            if (request.From != default)
            {
                query = query.Where(x => x.PlayerId == request.From);
            }

            var messages = query
                .WhereHasPermission(dbContext.Permissions, request.PlayerID)
                .OrderByDescending(x => x.Created)
                .Skip(request.Page * request.Size)
                .Take(request.Size)
                .ToList();

            return mapper.Map<List<MessageDTO>>(messages);
        }
    }
}
