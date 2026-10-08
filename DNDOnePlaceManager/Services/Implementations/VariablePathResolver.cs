using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DNDOnePlaceManager.Services.Implementations
{
    /// <summary>
    /// Walks a path like <c>.ResponseBody.results[0].name</c> into an object stored in action
    /// variables. Each segment can step into a POCO property (case-insensitive), a dictionary
    /// key, a JSON token, or a list index. A string that holds JSON (e.g. SendRequest's
    /// ResponseBody) is parsed on the fly when the path continues past it.
    /// </summary>
    public static class VariablePathResolver
    {
        // %v:varName.field.sub[0].x%
        private static readonly Regex _vPattern =
            new Regex(@"\%v:(\w+)((?:\.\w+|\[\d+\])+)\%", RegexOptions.Compiled);

        private static readonly Regex _segmentPattern =
            new Regex(@"\.(\w+)|\[(\d+)\]", RegexOptions.Compiled);

        /// <summary>Replaces every %v:var.path% token in <paramref name="raw"/>; unresolvable paths become empty.</summary>
        public static string ResolveTokens(string raw, Dictionary<string, object> variables)
        {
            if (string.IsNullOrEmpty(raw) || !raw.Contains("%v:"))
                return raw;

            return _vPattern.Replace(raw, m =>
            {
                if (!variables.TryGetValue(m.Groups[1].Value, out var root))
                    return string.Empty;
                return Stringify(Resolve(root, m.Groups[2].Value));
            });
        }

        /// <summary>Resolves <paramref name="path"/> (e.g. ".a.b[2]") against <paramref name="root"/>; null when any segment is missing.</summary>
        public static object Resolve(object root, string path)
        {
            var current = root;
            foreach (Match seg in _segmentPattern.Matches(path ?? string.Empty))
            {
                if (current == null)
                    return null;

                current = seg.Groups[1].Success
                    ? StepIntoMember(current, seg.Groups[1].Value)
                    : StepIntoIndex(current, int.Parse(seg.Groups[2].Value));
            }
            return current;
        }

        /// <summary>Turns a resolved value into the text substituted into a step argument.</summary>
        public static string Stringify(object value) => value switch
        {
            null => string.Empty,
            JValue jv => jv.Value?.ToString() ?? string.Empty,
            JToken jt => jt.ToString(Formatting.None),
            _ => value.ToString(),
        };

        private static object StepIntoMember(object current, string name)
        {
            current = ParseIfJsonString(current);

            switch (current)
            {
                case JObject jo:
                    return jo.GetValue(name, StringComparison.OrdinalIgnoreCase);
                case JToken:
                    return null;
                case IDictionary<string, object> dict:
                    foreach (var kv in dict)
                        if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
                            return kv.Value;
                    return null;
                case IDictionary legacyDict:
                    return legacyDict.Contains(name) ? legacyDict[name] : null;
            }

            var prop = current.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return prop?.GetValue(current);
        }

        private static object StepIntoIndex(object current, int index)
        {
            current = ParseIfJsonString(current);

            switch (current)
            {
                case JArray ja:
                    return index < ja.Count ? ja[index] : null;
                case string:
                    return null;
                case IList list:
                    return index < list.Count ? list[index] : null;
                case IEnumerable seq:
                    var i = 0;
                    foreach (var item in seq)
                        if (i++ == index)
                            return item;
                    return null;
                default:
                    return null;
            }
        }

        private static object ParseIfJsonString(object current)
        {
            if (current is JValue { Type: JTokenType.String } jv)
                current = jv.Value<string>();

            if (current is string s)
            {
                var trimmed = s.TrimStart();
                if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                {
                    try { return JToken.Parse(s); }
                    catch (JsonReaderException) { }
                }
            }
            return current;
        }
    }
}
