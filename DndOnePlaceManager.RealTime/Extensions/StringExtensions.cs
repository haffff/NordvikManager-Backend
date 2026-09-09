using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;

namespace DNDOnePlaceManager.Extensions
{
    public static class StringExtensions
    {
        private static Regex exFindKeyword = new Regex(@"\%(\w+)\%");
        public static string Prepare(this string str, Dictionary<string, object> variables)
        {
            if (str is null)
                return null;

            str = exFindKeyword.Replace(str, (match) =>
            {
                var word = match.Groups[1].Value;
                if (variables.ContainsKey(word))
                {
                    bool dto = false;
                    if (word.StartsWith("dto:"))
                    {
                        dto = true;
                        word = word.Replace("dto:", "");
                    }

                    if (variables.ContainsKey(word))
                    {
                        if (dto)
                        {
                            return JObject.FromObject(variables[word], new JsonSerializer() { ContractResolver = new CamelCasePropertyNamesContractResolver() }).ToString();
                        }
                        else
                        {
                            return variables[word]?.ToString();
                        }
                    }
                    else
                    {
                        return word;
                    }
                }
                return match.Value;
            });

            return str;
        }
    }
}
