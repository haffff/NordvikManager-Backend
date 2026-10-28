using NordvikManager.Application.Mapping;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Generic.Command;
using NordvikManager.Application.Generic.Handlers;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Map.GetFlatMaps
{
    internal class GetFlatMapsCommandHandler : GenericGetListHandler<GetFlatMapsCommand, MapModel, MapDTO>
    {
        public GetFlatMapsCommandHandler(IDbContext context, IMapper mapper) : base(context, mapper)
        {
        }

        public override IEnumerable<MapModel> GetEntities(GetFlatMapsCommand request)
        {
            return dbContext.Games
                .Include(x => x.Maps)
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
