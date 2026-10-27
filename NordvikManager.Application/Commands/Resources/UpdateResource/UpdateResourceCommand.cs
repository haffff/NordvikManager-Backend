using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.Auth;

namespace NordvikManager.Application.Commands.Resources.UpdateResource
{
    public class UpdateResourceCommand : CommandBase<CommandResponse>
    {
        public Guid? gameID { get; set; }
        public PlayerDTO? Player { get; set; }
        public User? User { get; set; }
        public ResourceDTO Resource { get; set; }
    }
}
