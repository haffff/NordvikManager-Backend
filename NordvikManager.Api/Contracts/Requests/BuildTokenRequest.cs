using System;

namespace NordvikManager.Models
{
    /// <summary>A token to build: of a card, or from a token file (and picture), placed at X, Y on a map.</summary>
    public class BuildTokenRequest
    {
        public Guid MapId { get; set; }
        public Guid? CardId { get; set; }
        public string? Token { get; set; }
        public string? Image { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}
