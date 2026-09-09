using System;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class GetDataStepData
    {
        [Description("Type of data to retrieve, e.g. 'Character', 'Item', 'Map'.")]
        public string? Type { get; set; }

        [Description("Name of the entity to retrieve. Used when looking up by name.")]
        public string? Name { get; set; }

        [Description("Name of a specific property to extract from the retrieved entity.")]
        public string? PropertyName { get; set; }

        [Description("Name of the variable where the retrieved data will be stored.")]
        public string? Output { get; set; }

        [Description("ID of the entity to retrieve. Used when looking up by ID instead of Name.")]
        public string? Id { get; set; }

        [Description("If true, retrieves a single element instead of a collection.")]
        public bool SingleElement { get; set; }
    }
}
