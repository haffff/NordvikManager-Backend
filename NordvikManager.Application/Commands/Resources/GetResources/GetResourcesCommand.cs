using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.Auth;
using MediatR;

namespace NordvikManager.Application.Commands.Resources
{
    public class GetResourcesCommand : CommandBase<(CommandResponse, List<ResourceDTO>)>
    {
        public Guid? GameId { get; set; }
        public PlayerDTO Player { get; set; }
        /// <summary>The caller runs this server, so may see where its files are.</summary>
        public bool IsLocalAdmin { get; set; }
    }
}
