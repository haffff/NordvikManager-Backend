using AutoMapper;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Layouts.RemoveLayout
{
    internal class RemoveLayoutCommandHandler : GenericDeleteHandler<RemoveLayoutCommand, LayoutModel>
    {
        public RemoveLayoutCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }
    }
}
