using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Generic.Command;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Card.AddCard
{
    public class AddCardCommand : GenericAddCommand<CardDto>
    {
        public bool IsTemplate { get; set; }
        public bool IsCustomUi { get; set; }
    }
}
