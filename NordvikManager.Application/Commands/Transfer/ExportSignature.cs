using NordvikManager.Application.DataTransferObjects.Transfer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NordvikManager.Application.Commands.Transfer
{
    /// <summary>
    /// Tells this server's exports from anyone else's: an export is signed with a secret only
    /// this server has (HMAC-SHA256), so a file can't claim to be from here by copying
    /// anything out of one of ours, and the signature says nothing about who made it.
    /// <para>
    /// The file comes back through the browser, which rewrites it (1.0 becomes 1, other
    /// spacing and escapes, nulls left out or written): the signature is over a canonical
    /// form, the same for all of those.
    /// </para>
    /// </summary>
    public static class ExportSignature
    {
        /// <summary>The <see cref="ServerSecrets.GetServerSecretCommand"/> name of the key.</summary>
        public const string SecretName = "export-signing";
        public const string Field = "signature";
        private const string Prefix = "hmac-sha256:";

        /// <summary>The file Export JSON gives (camelCase, nulls left out), with its signature.</summary>
        public static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        private static readonly JsonSerializerSettings ReadSettings = new() { DateParseHandling = DateParseHandling.None };

        /// <summary>The export as JSON, signed with <paramref name="secret"/>.</summary>
        public static string Write(EntityExportDto export, byte[] secret)
        {
            // Read back as text first, so it's signed as it will be read (dates as written).
            var json = Read(JsonConvert.SerializeObject(export, Settings));
            json.Remove(Field);
            json[Field] = Sign(json, secret);
            return json.ToString(Formatting.None);
        }

        /// <summary>The JSON of an export, read as <see cref="IsSignedWith"/> expects it (dates kept as text).</summary>
        public static JObject Read(string text) =>
            JsonConvert.DeserializeObject<JObject>(text, ReadSettings) ?? throw new JsonException("Empty.");

        /// <summary>Whether this export was signed with <paramref name="secret"/> and hasn't changed since.</summary>
        public static bool IsSignedWith(JObject json, byte[] secret)
        {
            if (json[Field] is not JValue { Type: JTokenType.String } value || ((string)value!) is not { } text || !text.StartsWith(Prefix))
                return false;

            byte[] given;
            try
            {
                given = Convert.FromBase64String(text[Prefix.Length..]);
            }
            catch (FormatException)
            {
                return false;
            }

            var unsigned = (JObject)json.DeepClone();
            unsigned.Remove(Field);
            return CryptographicOperations.FixedTimeEquals(given, Mac(unsigned, secret));
        }

        private static string Sign(JObject unsigned, byte[] secret) => Prefix + Convert.ToBase64String(Mac(unsigned, secret));

        private static byte[] Mac(JObject unsigned, byte[] secret)
        {
            var canonical = new StringBuilder();
            WriteCanonical(unsigned, canonical);
            return HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(canonical.ToString()));
        }

        // Keys in ordinal order, nulls left out, numbers as the doubles a browser reads them as,
        // strings escaped one way.
        private static void WriteCanonical(JToken token, StringBuilder into)
        {
            switch (token)
            {
                case JObject obj:
                    into.Append('{');
                    var first = true;
                    foreach (var property in obj.Properties().Where(p => p.Value.Type != JTokenType.Null).OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        if (!first) into.Append(',');
                        first = false;
                        into.Append(JsonConvert.ToString(property.Name));
                        into.Append(':');
                        WriteCanonical(property.Value, into);
                    }
                    into.Append('}');
                    break;
                case JArray array:
                    into.Append('[');
                    for (var i = 0; i < array.Count; i++)
                    {
                        if (i > 0) into.Append(',');
                        WriteCanonical(array[i], into);
                    }
                    into.Append(']');
                    break;
                case JValue value:
                    into.Append(Canonical(value));
                    break;
                default:
                    throw new JsonException($"Unexpected {token.Type} in an export.");
            }
        }

        private static string Canonical(JValue value)
        {
            switch (value.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return "null";
                case JTokenType.Boolean:
                    return (bool)value ? "true" : "false";
                case JTokenType.Integer:
                case JTokenType.Float:
                    // As a browser holds numbers: a double, in its shortest round-trip form (1 and
                    // 1.0 alike; a big integer as the double it becomes).
                    var number = value.Value is System.Numerics.BigInteger big ? (double)big : Convert.ToDouble(value.Value, CultureInfo.InvariantCulture);
                    return number.ToString("R", CultureInfo.InvariantCulture);
                case JTokenType.Date:
                    // Not from Read (dates stay text there); written as JSON would write it.
                    return JsonConvert.SerializeObject(value);
                default:
                    return JsonConvert.ToString(Convert.ToString(value.Value, CultureInfo.InvariantCulture));
            }
        }
    }
}
