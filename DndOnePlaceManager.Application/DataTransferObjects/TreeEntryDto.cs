namespace DndOnePlaceManager.Application.DataTransferObjects
{
    public class TreeEntryDto
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public Guid? ParentId { get; set; }
        public bool? IsFolder { get; set; }
        public bool? Head { get; set; }
        public Guid? Next { get; set; }
        public Guid? TargetId { get; set; }
        public string? EntryType { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public bool? AutoConnect { get; set; }
    }
}
