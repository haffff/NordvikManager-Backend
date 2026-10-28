using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Generic.Command
{
    public class GenericAddCommand<TDto> : GamePlayerCommandBase<(CommandResponse, Guid)> where TDto : IGameDataTransferObject
    {
        public TDto Dto { get; set; }

        /// <summary>
        /// The folder (tree entry id) the new entity's tree entry goes into; null for the top
        /// level. Only for handlers that make one (GenericAddHandlerWithTreeEntry).
        /// </summary>
        public Guid? TreeParentId { get; set; }
    }
}
