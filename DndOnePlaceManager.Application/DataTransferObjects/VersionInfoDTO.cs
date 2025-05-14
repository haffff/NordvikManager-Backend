namespace DndOnePlaceManager.Application.DataTransferObjects
{
    public class VersionInfoDTO
    {
        public string Version { get; set; }
        public string CurrentVersion { get; set; }
        public string[] Changes { get; set; }
        public bool IsUpdateAvailable { get; set; }
    }
}
