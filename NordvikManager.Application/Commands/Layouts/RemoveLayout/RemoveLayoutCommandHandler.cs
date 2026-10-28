using NordvikManager.Application.Mapping;
using NordvikManager.Application.Generic.Handlers;
using NordvikManager.Domain.Entities;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Application.Commands.Layouts.RemoveLayout
{
    internal class RemoveLayoutCommandHandler : GenericDeleteHandler<RemoveLayoutCommand, LayoutModel>
    {
        public RemoveLayoutCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }
    }
}
