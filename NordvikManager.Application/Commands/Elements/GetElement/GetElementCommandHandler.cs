
using NordvikManager.Application.Mapping;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Generic.Handlers;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Domain.Entities.BattleMap;

namespace NordvikManager.Application.Commands.Elements
{
    internal class GetElementCommandHandler : GenericGetHandler<GetElementCommand, ElementModel, ElementDTO>
    {
        public GetElementCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }
    }
}

