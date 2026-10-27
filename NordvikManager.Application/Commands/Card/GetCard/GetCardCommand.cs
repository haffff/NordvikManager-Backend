using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Generic.Command;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Card.GetCard
{
    public class GetCardCommand : GenericGetCommand<CardDto>
    {
        public string? Name { get; set; }
    }
}
