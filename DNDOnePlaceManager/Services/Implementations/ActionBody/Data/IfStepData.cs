namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class IfStepData
    {
        public string? Condition { get; set; }
        public string? TrueLabel { get; set; }
        public string? FalseLabel { get; set; }
        public string? OutputName { get; set; }
        public string ActionTrue { get; internal set; }
        public string ActionFalse { get; internal set; }
    }
}
