using NordvikManager.Application.Mapping;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Resources.Link
{
    internal class BrowseLocalDirectoryCommandHandler : HandlerBase<BrowseLocalDirectoryCommand, IReadOnlyList<LocalDirectoryEntry>>
    {
        private readonly IFileStorageProvider storage;

        public BrowseLocalDirectoryCommandHandler(IDbContext dbContext, IMapper mapper, IFileStorageProvider storage)
            : base(dbContext, mapper)
        {
            this.storage = storage;
        }

        public override async Task<IReadOnlyList<LocalDirectoryEntry>> Handle(BrowseLocalDirectoryCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            // The host's disk belongs to whoever runs this server, not to a game's GM.
            if (!request.IsLocalAdmin)
                throw new PermissionException(Permission.Edit);

            var playerId = request.Player?.Id ?? Guid.Empty;

            var game = await dbContext.Games
                .Include(g => g.Players)
                .FirstOrDefaultAsync(g => g.Id == request.GameId && g.Players.Any(p => p.Id == playerId), cancellationToken);
            Guard.NotFound(game, "Game", request.GameId);

            return storage.ListDirectory(request.Path);
        }
    }
}
