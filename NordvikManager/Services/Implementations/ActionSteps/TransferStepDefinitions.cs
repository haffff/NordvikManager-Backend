using NordvikManager.Application.Commands.Transfer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NordvikManager.Services.Implementations.ActionSteps
{
    // Both run as the system, like Create Card: the GM or addon that wrote the action chose
    // to copy or import. Import JSON still takes only the GM's (or an addon's) files.

    public class DuplicateStepDefinition : IActionStepDefinition
    {
        public string Name => "Duplicate";
        public string Value => "Duplicate";
        public IReadOnlyList<string> Keywords => new[] { "copy", "clone", "map", "card", "soundboard", "playlist" };
        public string Description => "Copies a map (with everything on it), card, template, custom view, playlist or soundboard, storing the copy's id in a variable.";
        public string Category => "Data";
        public string? Summary => "Copy {Kind} {Id}[ as {Name}][ → {Output}]";
        public Type DataType => typeof(DuplicateStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<DuplicateStepData>();
            if (!EntityTransferKinds.IsKnown(data.Kind))
                throw new ActionProcessException($"Duplicate: 'Kind' must be one of map, card, template, view, playlist, soundboard (got '{data.Kind}').");
            if (!Guid.TryParse(data.Id, out var id))
                throw new ActionProcessException($"Duplicate: 'Id' isn't an id ('{data.Id}').");

            var system = gameLobby.SystemPlayer;
            var export = await mediator.Send(new ExportEntityCommand { GameId = gameLobby.GameId, Player = system, Kind = data.Kind, Id = id })
                         ?? throw new ActionProcessException($"Duplicate: no {data.Kind} with id '{id}' in this game.");

            var folder = string.IsNullOrWhiteSpace(data.Folder) ? null : data.Folder;
            var result = await Run(() => mediator.Send(new ImportEntityCommand
            {
                GameId = gameLobby.GameId,
                Player = system,
                Export = export,
                Name = string.IsNullOrWhiteSpace(data.Name) ? $"{export.Name} (copy)" : data.Name,
                FolderPath = folder,
                AfterTargetId = folder == null ? id : null,
            }), Name);

            await EntityImportNotifier.NotifyAsync(mediator, gameLobby, system, result);
            if (!string.IsNullOrWhiteSpace(data.Output))
                variables[data.Output] = result.Id.ToString();
        }

        // The command's reasons (wrong file, wrong kind) as the step's own errors.
        internal static async Task<ImportEntityResult> Run(Func<Task<ImportEntityResult>> import, string step)
        {
            try
            {
                return await import();
            }
            catch (WrongArgumentsException e)
            {
                throw new ActionProcessException($"{step}: {e.Message}");
            }
            catch (ResourceNotFoundException e)
            {
                throw new ActionProcessException($"{step}: {e.Message}");
            }
        }
    }

    public class ImportJsonStepDefinition : IActionStepDefinition
    {
        public string Name => "Import JSON";
        public string Value => "ImportJson";
        public IReadOnlyList<string> Keywords => new[] { "import", "export", "json", "map", "soundboard", "ship", "addon" };
        public string Description => "Imports a map, card, playlist or soundboard from an exported file (Export JSON) in the game, e.g. one an addon ships, storing the new id in a variable.";
        public string Category => "Data";
        public string? Summary => "Import {Resource}[ into {Folder}][ → {Output}]";
        public Type DataType => typeof(ImportJsonStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<ImportJsonStepData>();
            if (string.IsNullOrWhiteSpace(data.Resource))
                throw new ActionProcessException("Import JSON: 'Resource' is required.");
            var kind = string.IsNullOrWhiteSpace(data.Kind) ? null : data.Kind;
            if (kind != null && !EntityTransferKinds.IsKnown(kind))
                throw new ActionProcessException($"Import JSON: 'Kind' must be one of map, card, template, view, playlist, soundboard (got '{kind}').");

            var system = gameLobby.SystemPlayer;
            var result = await DuplicateStepDefinition.Run(() => mediator.Send(new ImportEntityFromResourceCommand
            {
                GameId = gameLobby.GameId,
                Player = system,
                Resource = data.Resource,
                ExpectedKind = kind,
                Name = string.IsNullOrWhiteSpace(data.Name) ? null : data.Name,
                FolderPath = string.IsNullOrWhiteSpace(data.Folder) ? null : data.Folder,
            }), Name);

            await EntityImportNotifier.NotifyAsync(mediator, gameLobby, system, result);
            if (!string.IsNullOrWhiteSpace(data.Output))
                variables[data.Output] = result.Id.ToString();
            if (!string.IsNullOrWhiteSpace(data.MissingOutput))
                variables[data.MissingOutput] = string.Join(", ", result.Missing);
        }
    }
}
