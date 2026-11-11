using NordvikManager.Application.Commands.ServerSecrets;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.DataTransferObjects.Transfer;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Application.Mapping;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace NordvikManager.Application.Commands.Transfer
{
    /// <summary>
    /// What importing this export into the game would bring, read-only: whether this server
    /// wrote it (<see cref="ExportSignature"/>), and which cards bring code the game doesn't
    /// have (their page and other files, created from the copies in the file). A card's page
    /// runs the Card API as whoever opens it, so the GM accepts that code from elsewhere first.
    /// </summary>
    public class InspectImportCommand : CommandBase<ImportInspectionDto>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        /// <summary>The file as read by <see cref="ExportSignature.Read"/>.</summary>
        public JObject Json { get; set; }
        public string? ExpectedKind { get; set; }
    }

    internal class InspectImportCommandHandler : HandlerBase<InspectImportCommand, ImportInspectionDto>
    {
        private readonly IMediator mediator;

        public InspectImportCommandHandler(IDbContext dbContext, IMapper mapper, IMediator mediator) : base(dbContext, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<ImportInspectionDto> Handle(InspectImportCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var playerId = request.Player?.Id ?? Guid.Empty;
            var game = await dbContext.Games.FirstOrDefaultAsync(g => g.Id == request.GameId && g.Players.Any(p => p.Id == playerId), cancellationToken);
            Guard.NotFound(game, "Game", request.GameId);
            game.ThrowIfNoPermission(playerId, Permission.Edit);

            var export = EntityExports.From(request.Json, request.ExpectedKind);
            var secret = await mediator.Send(new GetServerSecretCommand { Name = ExportSignature.SecretName }, cancellationToken);
            var fromHere = ExportSignature.IsSignedWith(request.Json, secret);

            var cards = new List<EntityExportDto>();
            if (EntityTransferKinds.IsCard(export.Kind))
                cards.Add(export);
            foreach (var card in export.Cards ?? new())
            {
                // A token's card the game already has is used as it is (ImportTokenCards).
                if (Guid.TryParse((string?)card.Data?["id"], out var id)
                    && await dbContext.Cards.AnyAsync(c => c.Id == id && c.GameId == request.GameId, cancellationToken))
                    continue;
                cards.Add(card);
            }

            var code = new List<ImportedCodeDto>();
            foreach (var card in cards)
            {
                var files = await NewCodeFiles(request.GameId, card, cancellationToken);
                if (files.Count > 0)
                    code.Add(new ImportedCodeDto { Card = (string?)card.Data?["name"] ?? card.Name ?? "", Files = files });
            }

            return new ImportInspectionDto
            {
                Kind = export.Kind,
                Name = export.Name,
                FromThisServer = fromHere,
                Code = code,
                NeedsConfirmation = !fromHere && code.Count > 0,
            };
        }

        // The card's page and other files that importing would create from the copies in the
        // file, as ImportCard finds them: by id in the game, then by key, then from the copy.
        private async Task<List<string>> NewCodeFiles(Guid gameId, EntityExportDto card, CancellationToken cancellationToken)
        {
            var ids = new List<Guid>();
            if (Guid.TryParse((string?)card.Data?["mainResource"], out var main))
                ids.Add(main);
            foreach (var item in card.Data?["additionalResources"] as JArray ?? new JArray())
                if (Guid.TryParse((string?)item, out var extra))
                    ids.Add(extra);

            var files = new List<string>();
            foreach (var id in ids.Distinct())
            {
                var file = card.Resources?.FirstOrDefault(r => r.Id == id);
                if (file == null || string.IsNullOrEmpty(file.Data))
                    continue; // not in the file: left out, nothing new
                if (await dbContext.Resources.AnyAsync(r => r.Id == id && r.GameId == gameId, cancellationToken))
                    continue;
                if (!string.IsNullOrEmpty(file.Key) && await dbContext.Resources.AnyAsync(r => r.GameId == gameId && r.Key == file.Key, cancellationToken))
                    continue;
                files.Add(file.Name);
            }
            return files;
        }
    }
}
