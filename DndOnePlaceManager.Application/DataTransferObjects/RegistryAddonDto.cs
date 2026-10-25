namespace DndOnePlaceManager.Application.DataTransferObjects
{
    /// <summary>
    /// An entry of the addon registry (its addons.json "repository" list), as the Addons panel
    /// shows it: an addon's info.json fields plus what the registry adds (release link,
    /// categories, readme).
    /// </summary>
    public class RegistryAddonDto
    {
        public string? Key { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        public string? Author { get; set; }
        public string? License { get; set; }
        public string? RepositoryUrl { get; set; }
        public string? ReleaseUrl { get; set; }
        /// <summary>Keys of the registry categories it belongs to (see RegistryCategoryDto).</summary>
        public List<string>? Categories { get; set; }
        /// <summary>Raw URL of its markdown readme; read through addon/readme, never fetched by the client.</summary>
        public string? Readme { get; set; }
        public List<RegistryDependencyDto>? Dependencies { get; set; }
    }

    public class RegistryDependencyDto
    {
        public string? Key { get; set; }
        public string? Version { get; set; }
    }

    /// <summary>A category of the registry ("categories" in addons.json).</summary>
    public class RegistryCategoryDto
    {
        public string? Key { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>An addon's readme: the markdown and where it came from (relative links resolve against it).</summary>
    public class AddonReadmeDto
    {
        public string? Key { get; set; }
        public string? Url { get; set; }
        public string? Markdown { get; set; }
    }
}
