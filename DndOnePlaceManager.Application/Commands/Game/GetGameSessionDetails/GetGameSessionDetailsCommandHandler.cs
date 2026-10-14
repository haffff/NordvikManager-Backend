using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Game.GetGameSessionDetails
{
    internal class GetGameSessionDetailsCommandHandler : HandlerBase<GetGameSessionDetailsCommand, GetGameSessionDetailsResult?>
    {
        private readonly IFileStorageProvider storage;

        public GetGameSessionDetailsCommandHandler(IDbContext battleMapContext, IMapper mapper, IFileStorageProvider storage) : base(battleMapContext, mapper)
        {
            this.storage = storage;
        }

        public async override Task<GetGameSessionDetailsResult?> Handle(GetGameSessionDetailsCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var game = await dbContext.Games
                .Include(g => g.Properties)
                .FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

            if (game == null)
                return null;

            return new GetGameSessionDetailsResult
            {
                Name = game.Name ?? string.Empty,
                Summary = game.Properties?.FirstOrDefault(p => p.Name == "shortDescription")?.Value,
                Description = game.Properties?.FirstOrDefault(p => p.Name == "longDescription")?.Value,
                PasswordRequired = game.Password != null,
                IsPublic = game.IsPublic,
                Image = await GetCoverImageAsync(game.Id),
            };
        }

        // The cover image AddGameCommandHandler stores (resource key "game_image"), as a
        // data URL; null when the game has none or its file is gone.
        private async Task<string?> GetCoverImageAsync(Guid gameId)
        {
            var image = await dbContext.Resources.FirstOrDefaultAsync(r => r.GameId == gameId && r.Key == "game_image");
            if (image == null)
                return null;

            var data = image.Storage == ResourceStorageKind.Blob
                ? image.Data
                : await storage.ReadAsync(image.Path!);
            if (data == null)
                return null;

            return $"data:{image.MimeType.GetDescriptionValue()};base64,{Convert.ToBase64String(data)}";
        }
    }
}
