using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace NordvikManager.Application.Commands.Transfer
{
    /// <summary>Where exported things keep the files and cards they use.</summary>
    internal static class EntityTransferFields
    {
        // A map element's fabric object (see the frontend's LoadCanvas toObject).
        public const string ResourceId = "resourceId";
        public const string ResourceKey = "resourceKey";
        public const string CardId = "cardId";
        public const string IsToken = "isToken";

        /// <summary>The picture shown for an image whose file isn't there (frontend SYSTEM_ASSET_KEYS.EMPTY_IMAGE).</summary>
        public const string EmptyImageKey = "emptyImage";

        /// <summary>The card property naming the player a card belongs to.</summary>
        public const string PlayerOwner = "player_owner";

        public static readonly JsonSerializer Serializer = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        // Keys are short names without spaces (addon_file.png, emptyTokenImage), unlike most
        // property values (notes, numbers), which are never looked up as files.
        public static bool CouldBeKey(string? value) =>
            !string.IsNullOrEmpty(value) && value.Length <= 200 && !value.Any(char.IsWhiteSpace);
    }
}
