using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    // Every turn order step works on one map's turn order. MapId may be left empty when
    // the game has a single battle map view: its current map is used.
    public class TurnOrderMapStepData
    {
        [Description("Map whose turn order to change, e.g. %v:Data.mapId% in a Turn Changed hook. Leave empty to use the map shown in the game's only battle map view.")]
        public string? MapId { get; set; }
    }

    public class AddToTurnOrderStepData : TurnOrderMapStepData
    {
        [Description("Token (element) on that map to add. Leave empty to add a free entry with just a Name.")]
        public string? ElementId { get; set; }

        [Description("Name shown in the turn order. For a token it defaults to the token's name.")]
        public string? Name { get; set; }

        [Description("Initiative (a number), optional. Sort Turn Order puts the highest first.")]
        public string? Initiative { get; set; }

        [Description("\"true\" to hide the entry from players (the GM still runs its turn).")]
        public string? Hidden { get; set; }

        [VariableOutput]
        [Description("Name of the variable where the new entry's id will be stored.")]
        public string? Output { get; set; }
    }

    public class RemoveFromTurnOrderStepData : TurnOrderMapStepData
    {
        [Description("Id of the entry to remove. Or leave empty and give ElementId.")]
        public string? EntryId { get; set; }

        [Description("Token whose entry to remove.")]
        public string? ElementId { get; set; }
    }

    public class SetInitiativeStepData : TurnOrderMapStepData
    {
        [Description("Id of the entry. Or leave empty and give ElementId.")]
        public string? EntryId { get; set; }

        [Description("Token whose entry to change.")]
        public string? ElementId { get; set; }

        [Description("New initiative (a number). Leave empty to clear it.")]
        public string? Initiative { get; set; }

        [Description("\"true\" to sort the turn order afterwards (highest initiative first).")]
        public string? Sort { get; set; }
    }

    public class ResetTurnOrderStepData : TurnOrderMapStepData
    {
        [Description("\"true\" to remove every entry; otherwise the entries stay and round 1 starts again with the first one.")]
        public string? Clear { get; set; }
    }

    public class GetTurnOrderStepData : TurnOrderMapStepData
    {
        [VariableOutput]
        [Description("Name of the variable for the turn order: { MapId, Round, CurrentEntryId, Entries: [{ Id, Name, Initiative, ElementId, Hidden }] }.")]
        public string? Output { get; set; }
    }
}
