using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DNDOnePlaceManager.Extensions
{
    public static class StringExtensions
    {
        // %name% substitutes the variable's text; %dto:name% substitutes it serialized as
        // camelCase JSON (objects, lists, strings quoted) so it can be embedded in a JSON argument.
        private static Regex exFindKeyword = new Regex(@"\%((?:dto:)?\w+)\%");

        private static readonly JsonSerializer dtoSerializer = new JsonSerializer()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public static string Prepare(this string str, Dictionary<string, object> variables)
        {
            if (str is null)
                return null;

            str = exFindKeyword.Replace(str, (match) =>
            {
                var word = match.Groups[1].Value;
                bool dto = word.StartsWith("dto:");
                if (dto)
                    word = word["dto:".Length..];

                if (!variables.TryGetValue(word, out var value))
                    return match.Value;

                if (dto)
                    return value == null ? "null" : JToken.FromObject(value, dtoSerializer).ToString(Formatting.None);

                return value?.ToString();
            });

            return str;
        }
    }
}
