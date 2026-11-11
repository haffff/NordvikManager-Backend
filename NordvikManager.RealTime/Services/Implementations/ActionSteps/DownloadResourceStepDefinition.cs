using NordvikManager.Application.Commands.Properties.Proxy;
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
    /// <summary>
    /// Saves a URL's response as a game resource, byte for byte (images, files), with the
    /// same address rules and protected bearer token as Send Request.
    /// </summary>
    public class DownloadResourceStepDefinition : IActionStepDefinition
    {
        public string Name => "Download Resource";
        public string Value => "DownloadResource";
        public IReadOnlyList<string> Keywords => new[] { "download", "image", "file", "fetch", "url", "web" };
        public string Category => "Network";
        public string Description => "Downloads a URL (e.g. an image) and saves it as a game resource. A protected property can be sent as the Bearer token; it is never returned.";
        public string? Summary => "Download {TargetUrl} → resource {Key}[ → {Output}]";
        public Type DataType => typeof(DownloadResourceStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<DownloadResourceStepData>()!;
            if (string.IsNullOrWhiteSpace(data.TargetUrl))
                throw new ActionProcessException("DownloadResource: 'TargetUrl' is required: the URL to download.");
            if (string.IsNullOrWhiteSpace(data.Key))
                throw new ActionProcessException("DownloadResource: 'Key' is required: the resource to save it as.");

            // ParentId: a variable holding a Guid, or a Guid; otherwise the game (as in Send Request).
            Guid parentId = gameLobby.GameId;
            if (!string.IsNullOrWhiteSpace(data.ParentId))
            {
                if (variables.TryGetValue(data.ParentId, out var parentVar) && parentVar is Guid parentGuid)
                    parentId = parentGuid;
                else if (!Guid.TryParse(data.ParentId, out parentId))
                    parentId = gameLobby.GameId;
            }

            DownloadResourceResult result;
            try
            {
                result = await mediator.Send(new DownloadResourceCommand
                {
                    GameId = gameLobby.GameId,
                    Player = new PlayerDTO { Id = gameLobby.SystemPlayer.Id, Name = gameLobby.SystemPlayer.Name },
                    TargetUrl = data.TargetUrl,
                    ParentID = parentId,
                    BearerTokenPropertyName = data.BearerTokenPropertyName,
                    Key = data.Key,
                    Name = data.Name,
                    MimeType = data.MimeType,
                    ParentFolder = data.FolderId,
                });
            }
            catch (Exception)
            {
                // Refused, blocked, timed out or too large: later steps (an If on success) still run.
                result = new DownloadResourceResult { Success = false, StatusCode = 0 };
            }

            if (!string.IsNullOrWhiteSpace(data.Output))
                variables[data.Output] = result;
        }
    }
}
