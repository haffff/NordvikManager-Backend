using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Elements.Group
{
    /// <summary>
    /// Replaces map elements with one group element (Dto, the fabric group the client made
    /// of them). Their properties are kept on the group for <see cref="UngroupElementsCommand"/>;
    /// a player gets on the group only what they had on every part. Tokens can't be grouped:
    /// a token in a group would be no token (no card, UI, turn order) any more.
    /// </summary>
    public class GroupElementsCommand : CommandBase<(CommandResponse, Guid)>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public List<Guid> ElementIds { get; set; } = new();
        public ElementDTO Dto { get; set; }
    }

    /// <summary>
    /// Replaces a group element with its parts (Children, placed by the client). A part whose
    /// Id was one of the grouped elements gets that element's properties back; every part
    /// gets the group's rights. Answers the parts' new ids, in the order given.
    /// </summary>
    public class UngroupElementsCommand : CommandBase<(CommandResponse, List<Guid>)>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid GroupId { get; set; }
        public List<ElementDTO> Children { get; set; } = new();
    }
}
