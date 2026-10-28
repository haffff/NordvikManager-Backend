using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using MediatR;

namespace NordvikManager.Application.Commands.Card.UpdateCard
{
    public class UpdateCardCommand : CommandBase<CommandResponse>
    {
        public PlayerDTO Player { get; set; }
        public CardDto Dto { get; set; }
    }
}
