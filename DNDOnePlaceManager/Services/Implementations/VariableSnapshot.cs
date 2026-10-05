using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace DNDOnePlaceManager.Services.Implementations
{
    /// <summary>
    /// JSON copy of an action's variables, used by RunScript (as <c>vars</c>) and by the
    /// editor's run trace. A value that can't be serialized is left out rather than failing.
    /// </summary>
    public static class VariableSnapshot
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        });

        /// <param name="maxValueChars">
        /// Values whose JSON is longer than this are replaced by a truncated string, so a big
        /// response body doesn't flood the trace. Use int.MaxValue for no limit.
        /// </param>
        public static JObject Build(Dictionary<string, object> variables, int maxValueChars = int.MaxValue)
        {
            var root = new JObject();
            foreach (var kv in variables)
            {
                try
                {
                    var token = kv.Value == null ? JValue.CreateNull() : JToken.FromObject(kv.Value, Serializer);
                    if (maxValueChars != int.MaxValue)
                    {
                        var json = token.ToString(Formatting.None);
                        if (json.Length > maxValueChars)
                            token = new JValue(json.Substring(0, maxValueChars) + $"… ({json.Length} chars)");
                    }
                    root[kv.Key] = token;
                }
                catch (Exception)
                {
                    // e.g. a type with a throwing getter — leave it out
                }
            }
            return root;
        }
    }
}
