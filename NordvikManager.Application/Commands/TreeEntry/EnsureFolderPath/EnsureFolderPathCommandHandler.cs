using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Application.Mapping;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.TreeEntry.EnsureFolderPath
{
    internal class EnsureFolderPathCommandHandler : HandlerBase<EnsureFolderPathCommand, (Guid? FolderId, List<TreeEntryDto> Created)>
    {
        public const int MaxDepth = 10;
        public const int MaxNameLength = 100;

        public EnsureFolderPathCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<(Guid? FolderId, List<TreeEntryDto> Created)> Handle(EnsureFolderPathCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var names = (request.Path ?? string.Empty)
                .Split('/')
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

            if (names.Count == 0)
                return (null, new List<TreeEntryDto>());

            Guard.Argument(names.Count <= MaxDepth && names.All(x => x.Length <= MaxNameLength), nameof(request.Path));

            var game = await dbContext.Games
                .IncludeTree(request.EntryType)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == request.GameID, cancellationToken);

            Guard.NotFound(game, "Game", request.GameID);

            // Making folders needs Edit on the game, as it does from the Cards panel.
            game.ThrowIfNoPermission(request.Player?.Id ?? Guid.Empty, Permission.Edit);

            TreeEntryModel? parent = null;
            var created = new List<TreeEntryModel>();
            foreach (var name in names)
            {
                var folder = game.TreeEntries.FirstOrDefault(x =>
                    x.IsFolder &&
                    x.EntryType == request.EntryType &&
                    x.Parent?.Id == parent?.Id &&
                    string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

                if (folder == null)
                {
                    // Pending (NewItem), like a tree_add without AutoConnect: the next tree
                    // load links it after its siblings.
                    folder = new TreeEntryModel
                    {
                        Name = name,
                        IsFolder = true,
                        EntryType = request.EntryType,
                        Parent = parent,
                        NewItem = true,
                        Game = game,
                    };
                    game.TreeEntries.Add(folder);
                    created.Add(folder);
                }

                parent = folder;
            }

            if (created.Count > 0)
                dbContext.SaveChanges();

            return (parent!.Id, mapper.Map<List<TreeEntryDto>>(created));
        }
    }
}
