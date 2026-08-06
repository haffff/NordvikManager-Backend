namespace DndOnePlaceManager.Infrastructure.Interfaces
{
    public class LocalDirectoryEntry
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public long? Size { get; set; }
    }
}
