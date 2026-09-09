using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class CreateCardStepData
    {
        [Description("Display name for the new card.")]
        public string Name { get; set; }

        [Description("Id of the template card to create from (e.g. resolved via %qn:card-\"Template Name\".id%). Determines the card's bundle (MainResource/AdditionalResources) and seeds its initial properties.")]
        public string TemplateId { get; set; }

        [Description("Player id to grant Edit permission on the new card. Leave empty to skip.")]
        public string Owner { get; set; }

        [Description("Name of the variable where the new card's id will be stored.")]
        public string Output { get; set; }
    }
}
