using Newtonsoft.Json.Linq;

namespace NordvikManager.Application.Commands.Actions.BuildToken
{
    /// <summary>
    /// Where and for what a token is placed. <paramref name="CardId"/>, <paramref name="Name"/> and
    /// <paramref name="Size"/> come from a card (character_name, drop_token_size) when there is one.
    /// </summary>
    internal record TokenPlacement(int GridSize, double X, double Y, string MapId, string? Image, string? CardId, string Name, int? Size);

    /// <summary>
    /// How a token file becomes the canvas object placed on the map. The client has a copy for
    /// the token editor's preview (helpers/buildToken.js); token-build-cases.json holds the cases
    /// both are tested with — change them together.
    /// </summary>
    internal static class TokenBuild
    {
        // Same as the client's TokenManager: tokens on the token layer, their UI above it.
        private const int TokenLayer = 100;
        private const int TokenUiLayer = 110;

        public static BuiltToken Build(JObject template, TokenPlacement placement)
        {
            // A card's token is the card's size (drop_token_size, else 1); only one placed
            // without a card takes the file's.
            var size = placement.CardId != null
                ? (placement.Size is > 0 ? placement.Size.Value : 1)
                : (int.TryParse(template.Value<string>("size"), out var fileSize) && fileSize > 0 ? fileSize : 1);
            var side = (placement.GridSize > 0 ? placement.GridSize : 50) * size;

            var o = (JObject?)template["object"]?.DeepClone() ?? new JObject();
            o["name"] = placement.Name;
            o["left"] = placement.X;
            o["top"] = placement.Y;
            // The picture's own size isn't known here: the token is as wide as it should be at
            // scale 1, and the client fits the picture to that width when it loads it.
            o["width"] = side;
            o["height"] = side;
            o["scaleX"] = 1;
            o["scaleY"] = 1;
            o["cardId"] = placement.CardId;
            o["isToken"] = true;
            o["mapId"] = placement.MapId;
            o["layer"] = TokenLayer;
            o["resourceId"] = placement.Image;

            var data = (JObject?)template["tokenData"]?.DeepClone() ?? new JObject();
            data["cardId"] = placement.CardId;
            data["editableProps"] = template["editableProps"]?.DeepClone() ?? new JArray();
            data["assignableIcons"] = template["assignableIcons"]?.DeepClone() ?? new JArray();
            var deps = (JArray?)data["propDeps"] ?? new JArray();
            deps.Add(new JObject
            {
                ["dtoProperty"] = "tokenImage", ["objectProperty"] = "src", ["type"] = "string",
                ["source"] = placement.CardId != null ? "card" : "element",
            });
            data["propDeps"] = deps;
            o["tokenData"] = data;

            var ui = (JArray?)template["additions"]?.DeepClone() ?? new JArray();
            foreach (var element in ui.OfType<JObject>())
                element["layer"] = TokenUiLayer;
            o["tokenUiElements"] = ui;

            // A circle crop without a radius covers the token; the client fits it to the picture.
            if (o["clipPath"] is JObject clip && (string?)clip["type"] == "circle" && !((double?)clip["radius"] > 0))
                clip["radius"] = side / 2.0;

            return new BuiltToken(o, size);
        }
    }
}
