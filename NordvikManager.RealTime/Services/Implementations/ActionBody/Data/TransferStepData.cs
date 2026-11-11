using NordvikManager.Models;
using System.ComponentModel;

namespace NordvikManager.Services.Implementations.ActionBody.Data
{
    public class DuplicateStepData
    {
        [UIType("transferkind")]
        [Description("What to copy: map, card, template, view (custom view), playlist or soundboard.")]
        public string Kind { get; set; }

        [Description("Id of the map, card or playlist to copy, e.g. %q:card[Goblin].id% or a variable.")]
        public string Id { get; set; }

        [Description("Name of the copy. Leave empty for the original's name with \" (copy)\".")]
        public string Name { get; set; }

        [Description("Folder of its panel to put the copy in, e.g. \"Encounters/Forest\" (made where missing). Leave empty to put it right after the original.")]
        public string Folder { get; set; }

        [VariableOutput("value")]
        [Description("Name of the variable where the copy's id will be stored.")]
        public string Output { get; set; }
    }

    public class ImportJsonStepData
    {
        [UIType("resourceid")]
        [Description("The exported file (Export JSON) to import: its key (e.g. an addon's \"myaddon_dungeon.json\") or id. Only files uploaded by the GM or an addon are imported.")]
        public string Resource { get; set; }

        [UIType("transferkind")]
        [Description("Import only this kind (map, card, template, view, playlist, soundboard). Leave empty to take whatever the file holds.")]
        public string Kind { get; set; }

        [Description("Name for what's imported. Leave empty for the name in the file.")]
        public string Name { get; set; }

        [Description("Folder of its panel to put it in, e.g. \"Imported\" (made where missing). Leave empty for the top level.")]
        public string Folder { get; set; }

        [VariableOutput("value")]
        [Description("Name of the variable where the new map's, card's or playlist's id will be stored.")]
        public string Output { get; set; }

        [VariableOutput("value")]
        [Description("Name of a variable for the files the import didn't find (names, comma-separated; empty when none). Leave empty to skip.")]
        public string MissingOutput { get; set; }
    }
}
