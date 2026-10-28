using NordvikManager.Application.Commands.Actions.Find;
using Newtonsoft.Json.Linq;

namespace NordvikManager.Application.UnitTests.Commands.Actions
{
    // What an area covers: the same cases as the client's (area-cases.json is shared with
    // NordvikManagerFrontEnd), so Find and the highlighting on the map agree.
    public class AreaShapeTests
    {
        public static IEnumerable<object[]> Cases() =>
            JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Commands", "Actions", "area-cases.json")))["cases"]!
                .Select(c => new object[] { (string)c["name"]!, c.ToString() });

        [Theory]
        [MemberData(nameof(Cases))]
        public void Touches_AsTheSharedCasesSay(string name, string json)
        {
            var c = JObject.Parse(json);
            var a = c["area"]!;
            var area = new Area(
                (string)a["shape"]!, (double)a["x"]!, (double)a["y"]!,
                (double?)a["radius"] ?? 0, (double?)a["width"] ?? 0, (double?)a["height"] ?? 0, (double?)a["angle"] ?? 53,
                (double?)a["toward"]?["x"], (double?)a["toward"]?["y"]);
            var t = c["token"]!;

            Assert.True((bool)c["touches"]! == AreaShape.TouchesToken(area, (double)t["x"]!, (double)t["y"]!, (double)t["w"]!, (double)t["h"]!), name);
        }

        [Fact]
        public void ALineOrConeWithoutADirection_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => AreaShape.TouchesToken(new Area("cone", 0, 0, 100, 0, 0, 53, null, null), 10, 0, 50, 50));
        }

        [Fact]
        public void AnUnknownShape_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => AreaShape.TouchesToken(new Area("hex", 0, 0, 100, 0, 0, 53, null, null), 10, 0, 50, 50));
        }
    }
}
