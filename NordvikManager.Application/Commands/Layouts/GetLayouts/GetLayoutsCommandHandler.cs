using NordvikManager.Application.Mapping;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Extension;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Layouts.GetLayouts
{
    internal class GetLayoutsCommandHandler : HandlerBase<GetLayoutsCommand, List<LayoutDTO>>
    {

        public GetLayoutsCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public async override Task<List<LayoutDTO>> Handle(GetLayoutsCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var game = dbContext.Games.Include(x => x.Layouts).FirstOrDefault(x => x.Id == request.GameID);

            if (game == null || request.Player == null)
            {
                return null;
            }

            var itemsQuery = game.Layouts.WithPermission(request.Player.Id ?? Guid.Empty);

            if (request.Flat)
            {
                return itemsQuery.Select(x => new LayoutDTO()
                {
                    Id = x.Id,
                    Name = x.Name,
                    Default = x.Default,
                    GameModelId = x.GameModelId,
                }).ToList();
            }
            else
            {
                return itemsQuery.Select(x =>
                {
                    var dto = mapper.Map<LayoutDTO>(x);
                    dto.Permission = x.GetPermission(request.Player.Id ?? Guid.Empty);
                    return dto;
                }).ToList();
            }
        }
    }
}
