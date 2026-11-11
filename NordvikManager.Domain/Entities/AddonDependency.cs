namespace NordvikManager.Domain.Entities
{
    /// <summary>
    /// Another addon an addon needs, as its info.json lists it: the addon's key, and the
    /// lowest version that works (none = any version). Stored with the addon, so uninstalling
    /// can tell which addons still need it.
    /// </summary>
    public class AddonDependency
    {
        public string Key { get; set; } = string.Empty;
        public string? Version { get; set; }
    }
}
