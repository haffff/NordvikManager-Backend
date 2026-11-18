using NordvikManager.WebRTC;
using System.Text;
using System.Text.Json;
using Xunit;

namespace NordvikManager.Tests.WebRTC
{
    // A player client splits a large message every 15 000 UTF-16 chars, which can cut an emoji's
    // surrogate pair; JSON.stringify then writes each half as an escape (\ud83c, \udfb2).
    // Decoding each chunk on its own turned a lone half into U+FFFD (Newtonsoft does), so the
    // message changed. The chunks' data stays escaped until they are joined.
    public class ChunkEnvelopeTests
    {
        // As the player client sends a chunk: JSON.stringify writes a lone surrogate as an escape.
        private static byte[] Chunk(string id, int index, int total, string data)
            => Encoding.UTF8.GetBytes("{\"type\":\"chunk\",\"chunkId\":\"" + id + "\",\"index\":" + index + ",\"total\":" + total + ",\"data\":" + JsLiteral(data) + "}");

        private static string JsLiteral(string s)
        {
            var b = new StringBuilder("\"");
            foreach (var c in s)
            {
                if (c == '"' || c == '\\') b.Append('\\').Append(c);
                else if (c < 0x20 || char.IsSurrogate(c)) b.Append("\\u").Append(((int)c).ToString("x4"));
                else b.Append(c);
            }
            return b.Append('"').ToString();
        }

        [Fact]
        public void ASurrogatePairSplitAcrossChunks_ComesBackWhole()
        {
            var message = "{\"command\":\"chat_push\",\"data\":\"roll 🎲 now\"}";
            var cut = message.IndexOf("🎲", System.StringComparison.Ordinal) + 1; // between the two halves
            Assert.True(char.IsHighSurrogate(message[cut - 1]));

            Assert.True(ChunkEnvelope.TryParse(Chunk("c1", 0, 2, message[..cut]), out var first));
            Assert.True(ChunkEnvelope.TryParse(Chunk("c1", 1, 2, message[cut..]), out var second));

            Assert.Equal(message, ChunkEnvelope.Decode(first!.Data + second!.Data));
        }

        [Fact]
        public void EscapesAndUnicode_Survive()
        {
            var message = "{\"a\":\"quote \\\" backslash \\\\ tab\\t zażółć\"}\n";

            Assert.True(ChunkEnvelope.TryParse(Chunk("c2", 0, 1, message), out var chunk));

            Assert.Equal("c2", chunk!.ChunkId);
            Assert.Equal(0, chunk.Index);
            Assert.Equal(1, chunk.Total);
            Assert.Equal(message, ChunkEnvelope.Decode(chunk.Data));
        }

        [Fact]
        public void FieldsInAnyOrder_AndOtherFields_AreFine()
        {
            var bytes = Encoding.UTF8.GetBytes("{\"data\":\"x\",\"total\":3,\"extra\":{\"n\":[1,2]},\"index\":2,\"chunkId\":\"c3\",\"type\":\"chunk\"}");

            Assert.True(ChunkEnvelope.TryParse(bytes, out var chunk));
            Assert.Equal((2, 3, "x"), (chunk!.Index, chunk.Total, ChunkEnvelope.Decode(chunk.Data)));
        }

        [Theory]
        [InlineData("{\"type\":\"api-request\",\"id\":\"1\"}")]
        [InlineData("{\"command\":\"chat_push\",\"data\":\"hi\"}")]
        [InlineData("{\"type\":\"chunk\",\"index\":0,\"total\":1,\"data\":\"x\"}")]            // no chunkId
        [InlineData("{\"type\":\"chunk\",\"chunkId\":\"c\",\"total\":1,\"data\":\"x\"}")]      // no index
        [InlineData("{\"type\":\"chunk\",\"chunkId\":\"c\",\"index\":0,\"total\":0,\"data\":\"x\"}")]
        [InlineData("{\"type\":\"chunk\",\"chunkId\":\"c\",\"index\":0,\"total\":1}")]         // no data
        [InlineData("{\"type\":\"chunk\",\"chunkId\":\"c\",\"index\":0,\"total\":1,\"data\":5}")]
        [InlineData("not json")]
        [InlineData("[1,2]")]
        [InlineData("")]
        public void NotAChunk_IsNotOne(string text)
        {
            Assert.False(ChunkEnvelope.TryParse(Encoding.UTF8.GetBytes(text), out _));
        }

        [Fact]
        public void Decode_OfALoneSurrogate_KeepsIt()
        {
            // A message that really ends mid-pair (a broken client) still decodes, as before.
            Assert.Equal("a\ud83c", ChunkEnvelope.Decode("a\\ud83c"));
        }

        [Fact]
        public void Decode_OfMalformedData_Throws()
        {
            Assert.ThrowsAny<JsonException>(() => ChunkEnvelope.Decode("bad \\x escape"));
        }
    }
}
