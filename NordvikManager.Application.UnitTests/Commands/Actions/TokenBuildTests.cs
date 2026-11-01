using NordvikManager.Application.Commands.Actions.BuildToken;
using Newtonsoft.Json.Linq;

namespace NordvikManager.Application.UnitTests.Commands.Actions
{
    // How a token file becomes the object placed on the map: the same cases as the client's
    // (token-build-cases.json is shared with NordvikManagerFrontEnd), so the token editor's
    // preview shows what placing the token will.
    public class TokenBuildTests
    {
        public static IEnumerable<object[]> Cases() =>
            JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Commands", "Actions", "token-build-cases.json")))["cases"]!
                .Select(c => new object[] { (string)c["name"]!, c.ToString() });

        [Theory]
        [MemberData(nameof(Cases))]
        public void Build_AsTheSharedCasesSay(string name, string json)
        {
            var c = JObject.Parse(json);
            var o = c["options"]!;
            var placement = new TokenPlacement(
                (int?)o["gridSize"] ?? 0, (double?)o["x"] ?? 0, (double?)o["y"] ?? 0, (string)o["mapId"]!,
                (string?)o["image"], (string?)o["cardId"], (string?)o["name"] ?? "Token", (int?)o["size"]);

            var built = TokenBuild.Build((JObject)c["template"]!, placement);

            var expected = c["expected"]!;
            Assert.Equal((int)expected["size"]!, built.Size);
            var want = Plain(expected["object"]!);
            var got = Plain(built.Object);
            Assert.True(JToken.DeepEquals(want, got), $"{name}\nexpected {want}\ngot      {got}");
        }

        [Fact]
        public void Build_LeavesTheTokenFileAsItWas()
        {
            var template = JObject.Parse(@"{ ""object"": { ""type"": ""image"" }, ""tokenData"": { ""propDeps"": [] }, ""additions"": [ { ""name"": ""bar"" } ] }");
            var before = template.ToString();

            TokenBuild.Build(template, new TokenPlacement(50, 0, 0, "map-1", null, null, "Token", null));

            Assert.Equal(before, template.ToString());
        }

        // The same JSON can differ in JToken.DeepEquals: 30 and 30.0, and a null string vs null.
        private static JToken Plain(JToken token) => token switch
        {
            JValue { Value: null } => JValue.CreateNull(),
            JValue { Type: JTokenType.Integer } v => new JValue(Convert.ToDouble(v.Value)),
            JObject obj => new JObject(obj.Properties().Select(p => new JProperty(p.Name, Plain(p.Value)))),
            JArray arr => new JArray(arr.Select(Plain)),
            _ => token.DeepClone(),
        };
    }
}
