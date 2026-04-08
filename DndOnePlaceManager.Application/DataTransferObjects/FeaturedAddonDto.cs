namespace DndOnePlaceManager.Application.DataTransferObjects
{
    /// <summary>
    /// Safe public view of an addon entry from the repository.
    /// ReleaseUrl and RepositoryUrl are intentionally omitted — clients install by key only.
    /// </summary>
    public class FeaturedAddonDto
    {
        public string? Name { get; set; }
        public string? Key { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        public string? Author { get; set; }
        public string? Website { get; set; }
        public string? License { get; set; }
        public List<string>? Dependencies { get; set; }
    }
}
