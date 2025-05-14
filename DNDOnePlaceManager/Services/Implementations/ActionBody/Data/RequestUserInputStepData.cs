using System;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class RequestUserInputStepData
    {
        public string? UserName { get; set; }
        public string? UserID { get; set; }
        public string? Message { get; set; }
        public TimeSpan? Timeout { get; set; }
        public string? Output { get; set; }
    }
}
