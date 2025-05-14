using System;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class GetDataStepData
    {
        public string? Type { get; set; }
        public string? Name { get; set; }
        public string? PropertyName { get; set; }
        public string? Output { get; set; }
        public string? Id { get; set; }
        public bool SingleElement { get; set; }
    }
}
