using NordvikManager.Application.Commands.ServerSecrets;
using NordvikManager.Application.Commands.Transfer;
using NordvikManager.Application.DataTransferObjects.Transfer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NordvikManager.Application.UnitTests.Commands.Transfer
{
    // An export says whether this server wrote it: signed with a secret only the server has.
    // The file goes through the browser (parsed, maybe rewritten) on its way back.
    public class ExportSignatureTests : SqliteHandlerTestBase
    {
        private readonly byte[] secret = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

        private static EntityExportDto Card() => new()
        {
            Kind = EntityTransferKinds.Card,
            Name = "Hero",
            Data = new JObject
            {
                ["name"] = "Hero",
                ["scale"] = 1.0,
                ["opacity"] = 0.1,
                ["big"] = 1e21,
                ["huge"] = 1e20,
                ["created"] = new DateTime(2026, 11, 11, 10, 30, 0, DateTimeKind.Utc),
                ["note"] = "café \"quoted\"",
                ["tags"] = new JArray("a", "b"),
            },
            Resources = new List<ExportedResourceDto> { new() { Id = Guid.NewGuid(), Name = "sheet.html", MimeType = "text/html", Data = "PGgxPg==" } },
        };

        private static JObject Parse(string text) =>
            JsonConvert.DeserializeObject<JObject>(text, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None })!;

        // What JSON.parse + JSON.stringify make of it: 1.0 becomes 1, other spacing, keys in
        // another order (a hand-edited file), \u escapes.
        private static JObject ThroughTheBrowser(string text)
        {
            var json = Parse(text);
            var data = (JObject)json["data"]!;
            data["scale"] = new JValue(1L);
            data["huge"] = new JValue(System.Numerics.BigInteger.Parse("100000000000000000000"));
            var reordered = new JObject(json.Properties().Reverse());
            return Parse(reordered.ToString(Formatting.Indented).Replace("é", "\\u00e9"));
        }

        [Fact]
        public void Signed_ByThisServer_IsRecognised()
        {
            var text = ExportSignature.Write(Card(), secret);

            Assert.StartsWith("hmac-sha256:", (string?)Parse(text)["signature"]);
            Assert.True(ExportSignature.IsSignedWith(Parse(text), secret));
        }

        [Fact]
        public void Signed_StillRecognised_AfterTheBrowserRewritesIt()
        {
            var text = ExportSignature.Write(Card(), secret);

            Assert.True(ExportSignature.IsSignedWith(ThroughTheBrowser(text), secret));
        }

        [Fact]
        public void Signed_StillRecognised_WithNullsWrittenOut()
        {
            var json = Parse(ExportSignature.Write(Card(), secret));
            json["cards"] = JValue.CreateNull();

            Assert.True(ExportSignature.IsSignedWith(json, secret));
        }

        [Fact]
        public void Changed_AfterSigning_IsNotRecognised()
        {
            var json = Parse(ExportSignature.Write(Card(), secret));
            json["resources"]![0]!["data"] = "PHNjcmlwdD4=";

            Assert.False(ExportSignature.IsSignedWith(json, secret));
        }

        [Fact]
        public void ADateChangedAfterSigning_IsNotRecognised()
        {
            var json = Parse(ExportSignature.Write(Card(), secret));
            var created = (string)json["data"]!["created"]!;
            json["data"]!["created"] = created.Replace("10:30", "10:31");

            Assert.False(ExportSignature.IsSignedWith(json, secret));
        }

        [Fact]
        public void SignedByAnotherServer_IsNotRecognised()
        {
            var theirs = Enumerable.Repeat((byte)7, 32).ToArray();

            Assert.False(ExportSignature.IsSignedWith(Parse(ExportSignature.Write(Card(), theirs)), secret));
        }

        [Fact]
        public void Unsigned_OrWithAJunkSignature_IsNotRecognised()
        {
            var json = Parse(ExportSignature.Write(Card(), secret));
            json.Remove("signature");
            Assert.False(ExportSignature.IsSignedWith(json, secret));

            json["signature"] = "hmac-sha256:not base64!";
            Assert.False(ExportSignature.IsSignedWith(json, secret));

            json["signature"] = 5;
            Assert.False(ExportSignature.IsSignedWith(json, secret));
        }

        [Fact]
        public void Write_KeepsTheExportReadable()
        {
            var json = Parse(ExportSignature.Write(Card(), secret));

            var export = json.ToObject<EntityExportDto>(EntityTransferFields.Serializer)!;
            Assert.Equal("nordvik-entity", export.Format);
            Assert.Equal("Hero", (string?)export.Data["name"]);
            Assert.Equal("sheet.html", export.Resources.Single().Name);
        }

        [Fact]
        public async Task ServerSecret_IsMadeOnceAndKept()
        {
            var first = await new GetServerSecretCommandHandler(Db, Mapper).Handle(new GetServerSecretCommand { Name = "export-signing" }, CancellationToken.None);
            var again = await new GetServerSecretCommandHandler(SeedContext(), Mapper).Handle(new GetServerSecretCommand { Name = "export-signing" }, CancellationToken.None);
            var other = await new GetServerSecretCommandHandler(Db, Mapper).Handle(new GetServerSecretCommand { Name = "something-else" }, CancellationToken.None);

            Assert.Equal(32, first.Length);
            Assert.Equal(first, again);
            Assert.NotEqual(first, other);
        }
    }
}
