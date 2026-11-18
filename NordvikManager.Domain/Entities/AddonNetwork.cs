namespace NordvikManager.Domain.Entities
{
    /// <summary>
    /// Where an addon's code connects outside NordvikManager, as its info.json "network" says:
    /// fixed origins (Hosts), and/or an address the user enters (UserHosts, saying what it is).
    /// Empty = nowhere. An addon without one (null on AddonModel) doesn't say.
    /// </summary>
    public class AddonNetwork
    {
        public List<string>? Hosts { get; set; }
        public string? UserHosts { get; set; }
    }
}
