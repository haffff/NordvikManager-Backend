using NordvikManager.Application.Commands.Resources.GetResourceContent;
using NordvikManager.Application.DataTransferObjects.Transfer;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Application.Mapping;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace NordvikManager.Application.Commands.Transfer
{
    internal class ImportEntityFromResourceCommandHandler : HandlerBase<ImportEntityFromResourceCommand, ImportEntityResult>
    {
        private readonly IMediator mediator;

        public ImportEntityFromResourceCommandHandler(IDbContext dbContext, IMapper mapper, IMediator mediator) : base(dbContext, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<ImportEntityResult> Handle(ImportEntityFromResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            Guard.Argument(!string.IsNullOrWhiteSpace(request.Resource), nameof(request.Resource));
            var byId = Guid.TryParse(request.Resource, out var id);
            var resource = await dbContext.Resources
                .Where(r => r.GameId == request.GameId && (byId ? r.Id == id : r.Key == request.Resource))
                .Select(r => new { r.Id, r.PlayerId, r.Name })
                .FirstOrDefaultAsync(cancellationToken);
            Guard.NotFound(resource, "Resource", request.Resource);

            var game = await dbContext.Games.FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);
            Guard.NotFound(game, "Game", request.GameId);
            var trusted = resource.PlayerId == game.SystemPlayerId
                || resource.PlayerId == game.MasterId
                || game.HasPermission(resource.PlayerId, Permission.Edit);
            if (!trusted)
                throw WrongArgumentsException.Because($"\"{resource.Name}\" was uploaded by a player: an action imports only files from the GM or an addon.");

            var content = await mediator.Send(new GetResourceContentCommand { GameID = request.GameId, ID = resource.Id, Player = request.Player }, cancellationToken);
            if (content?.Data == null)
                throw WrongArgumentsException.Because($"\"{resource.Name}\" can't be read.");

            EntityExportDto? export;
            try
            {
                export = JObject.Parse(Encoding.UTF8.GetString(content.Data)).ToObject<EntityExportDto>(EntityTransferFields.Serializer);
            }
            catch (JsonException)
            {
                export = null;
            }
            if (export == null)
                throw WrongArgumentsException.Because($"\"{resource.Name}\" isn't an exported map, card or playlist.");

            return await mediator.Send(new ImportEntityCommand
            {
                GameId = request.GameId,
                Player = request.Player,
                Export = export,
                ExpectedKind = request.ExpectedKind,
                Name = request.Name,
                FolderPath = request.FolderPath,
            }, cancellationToken);
        }
    }
}
