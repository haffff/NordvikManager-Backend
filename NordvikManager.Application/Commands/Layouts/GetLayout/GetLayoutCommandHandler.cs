using NordvikManager.Application.Mapping;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Generic.Handlers;
using NordvikManager.Domain.Entities;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Layouts.GetLayout
{
    internal class GetLayoutCommandHandler : GenericGetHandler<GetLayoutCommand, LayoutModel, LayoutDTO>
    {
        public GetLayoutCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }
    }
}
