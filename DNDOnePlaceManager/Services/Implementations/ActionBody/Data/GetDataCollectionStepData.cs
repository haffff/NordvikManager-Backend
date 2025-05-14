namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class GetDataCollectionStepData
    {
        public string InputValue { get; set; }
        public string FilterBy { get; set; }
        public string FilterType { get; set; }
        public string FilterValue { get; set; }
        public string SortBy { get; set; }
        public bool Descending { get; set; }
        public string OutputName { get; set; }
    }
}
