using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using MediatR;
using System;

namespace NordvikManager.Application.Commands.Actions
{
    public class UpdateActionCommand : CommandBase<CommandResponse>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public ActionDto Action { get; set; }
    }
}
