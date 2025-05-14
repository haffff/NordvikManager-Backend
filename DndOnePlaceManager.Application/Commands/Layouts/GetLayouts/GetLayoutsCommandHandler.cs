using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Layouts.GetLayouts
{
    internal class GetLayoutsCommandHandler : HandlerBase<GetLayoutsCommand, List<LayoutDTO>>
    {

        public GetLayoutsCommandHandler(IDbContext dbContext,  IMapper mapper) : base(dbContext, mapper)
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
                return itemsQuery.Select(x => new LayoutDTO() {
                    Id = x.Id,
                    Name = x.Name,
                    Default = x.Default,
                }).ToList();
            }
            else
            {
                return itemsQuery.Select(x => {
                     var dto = mapper.Map<LayoutDTO>(x);
                     dto.Permission = x.GetPermission(request.Player.Id ?? Guid.Empty);
                     return dto;
                 }).ToList();
            }
        }
    }
}
