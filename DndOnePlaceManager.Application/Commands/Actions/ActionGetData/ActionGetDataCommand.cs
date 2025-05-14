using DndOnePlaceManager.Application.DataTransferObjects;

namespace DndOnePlaceManager.Application.Commands.Actions.ActionGetData
{
    public class ActionGetDataCommand : CommandBase<List<IGameDataTransferObject>>
    {
        /// <summary>
        /// Game Id
        /// </summary>
        public Guid GameID { get; set; }

        /// <summary>
        /// Required, entity type ended with Model. Fge ElementModel
        /// </summary>
        public string EntityType { get; set; }

        /// <summary>
        /// Optional, Searches for specific Id
        /// </summary>
        public Guid? ID { get; set; }

        /// <summary>
        /// Optional, Searches for specific Name
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Optional, Searches for specific Property inside of element if applicable
        /// </summary>
        public string? Property { get; set; }
    }
}
