using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Map.GetFlatMaps
{
    internal class GetFlatMapsCommandHandler : GenericGetListHandler<GetFlatMapsCommand, MapModel, MapDTO>
    {
        public GetFlatMapsCommandHandler(IDbContext context, IMapper mapper) : base(context, mapper)
        {
        }

        public override IEnumerable<MapModel> GetEntities(GetFlatMapsCommand request)
        {
            return dbContext.Games
                .Include(x=>x.Maps)
                .FirstOrDefault(x => x.Id == request.GameID)?
                .Maps
                .ToList() ?? Enumerable.Empty<MapModel>();
        }

        public override List<MapDTO> ModifyOutput(List<MapDTO> response)
        {
            //Flatten the collection to not include the map data
            return response.Select(x => new MapDTO() { Name = x.Name, Id = x.Id }).ToList();
        }
    }
}
