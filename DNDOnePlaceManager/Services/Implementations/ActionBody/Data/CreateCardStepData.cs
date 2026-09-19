using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class CreateCardStepData
    {
        [Description("Display name for the new card.")]
        public string Name { get; set; }

        [Description("Id of the template card to create from (e.g. resolved via %qn:card-\"Template Name\".id%). Determines the card's bundle (MainResource/AdditionalResources) and seeds its initial properties.")]
        public string TemplateId { get; set; }

        [UIType("playerid")]
        [Description("Player to grant Edit permission on the new card. Pick one, or type an ID / %variable%. Leave empty to skip.")]
        public string Owner { get; set; }

        [Description("Name of the variable where the new card's id will be stored.")]
        public string Output { get; set; }

        [Description("If true, the new card is created as a reusable Template (IsTemplate=true) instead of a regular Instance — e.g. for an addon that imports/generates a new card blueprint at runtime. Defaults to false (a normal instance).")]
        public bool IsTemplate { get; set; }
    }
}
