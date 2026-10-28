using NordvikManager.Application.DataTransferObjects.Game;
using Newtonsoft.Json.Linq;

namespace NordvikManager.Application.Commands.Actions.BuildToken
{
    /// <summary>
    /// Builds a token's canvas object, as the client does when a token is placed: from a card
    /// (its "token" file, tokenImage, drop_token_size, character_name) or straight from a
    /// token file. Nothing is saved; the caller adds it to the map.
    /// </summary>
    public class BuildTokenCommand : CommandBase<BuiltToken>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; } = null!;
        public Guid MapId { get; set; }
        public Guid? CardId { get; set; }

        /// <summary>A token file (resource key or id), when there is no card.</summary>
        public string? Token { get; set; }

        /// <summary>The picture (resource key or id), when there is no card.</summary>
        public string? Image { get; set; }

        public double X { get; set; }
        public double Y { get; set; }
    }

    /// <summary>The token's canvas object and its size in squares.</summary>
    public record BuiltToken(JObject Object, int Size);
}
