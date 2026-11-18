using System;
using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NordvikManager.WebRTC
{
    /// <summary>
    /// One part of a large message a player sent: <c>{ type: "chunk", chunkId, index, total, data }</c>.
    /// <see cref="Data"/> stays as written in the JSON (escaped): the client splits every 15 000
    /// UTF-16 chars, which can cut a surrogate pair, and JSON.stringify writes each half as an
    /// escape. Decoding a lone half on its own gives U+FFFD (Newtonsoft does), so the parts are
    /// joined first and decoded once (<see cref="Decode"/>), when the halves are together again.
    /// </summary>
    internal sealed record ChunkEnvelope(string ChunkId, int Index, int Total, string Data)
    {
        /// <summary>The chunk in this UTF-8 message, or false when it isn't one.</summary>
        public static bool TryParse(ReadOnlySpan<byte> utf8, out ChunkEnvelope? chunk)
        {
            chunk = null;
            var start = 0;
            while (start < utf8.Length && utf8[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
                start++;
            if (start == utf8.Length || utf8[start] != (byte)'{')
                return false;

            string? type = null, chunkId = null, data = null;
            int? index = null, total = null;
            try
            {
                var reader = new Utf8JsonReader(utf8);
                reader.Read(); // {
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    var name = reader.GetString();
                    reader.Read();
                    switch (name)
                    {
                        case "type":
                            type = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                            break;
                        case "chunkId":
                            chunkId = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                            break;
                        case "index":
                            index = reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var i) ? i : null;
                            break;
                        case "total":
                            total = reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var t) ? t : null;
                            break;
                        case "data":
                            // The string as written, escapes and all.
                            data = reader.TokenType == JsonTokenType.String
                                ? Encoding.UTF8.GetString(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan)
                                : null;
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                return false;
            }

            if (type != "chunk" || string.IsNullOrEmpty(chunkId) || index == null || total is null or <= 0 || data == null)
                return false;
            chunk = new ChunkEnvelope(chunkId, index.Value, total.Value, data);
            return true;
        }

        /// <summary>
        /// The text of joined <see cref="Data"/>: JSON string escapes undone, surrogate halves
        /// put back together (a lone one is kept as is). Throws <see cref="JsonException"/> on a
        /// malformed escape.
        /// </summary>
        public static string Decode(string escaped)
        {
            if (escaped.IndexOf('\\') < 0)
                return escaped;

            var text = new StringBuilder(escaped.Length);
            for (var i = 0; i < escaped.Length; i++)
            {
                var c = escaped[i];
                if (c != '\\')
                {
                    text.Append(c);
                    continue;
                }
                if (++i == escaped.Length)
                    throw new JsonException("A chunked message ends in the middle of an escape.");
                switch (escaped[i])
                {
                    case '"': text.Append('"'); break;
                    case '\\': text.Append('\\'); break;
                    case '/': text.Append('/'); break;
                    case 'b': text.Append('\b'); break;
                    case 'f': text.Append('\f'); break;
                    case 'n': text.Append('\n'); break;
                    case 'r': text.Append('\r'); break;
                    case 't': text.Append('\t'); break;
                    case 'u':
                        if (i + 4 >= escaped.Length || !ushort.TryParse(escaped.AsSpan(i + 1, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code))
                            throw new JsonException("A chunked message has a malformed \\u escape.");
                        text.Append((char)code);
                        i += 4;
                        break;
                    default:
                        throw new JsonException($"A chunked message has an unknown escape \\{escaped[i]}.");
                }
            }
            return text.ToString();
        }
    }
}
