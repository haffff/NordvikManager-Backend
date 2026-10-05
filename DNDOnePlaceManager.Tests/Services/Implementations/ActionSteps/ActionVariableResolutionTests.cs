using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Services.Implementations;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // Covers %v:var.path% (nested paths, list indexes, JSON-in-a-string such as SendRequest's
    // ResponseBody) and %dto:var% (JSON serialization), both of which used to be unreachable:
    // %v:% only took one level and %dto:% never matched the \w+ token regex.
    public class ActionVariableResolutionTests
    {
        private class Response
        {
            public int StatusCode { get; set; }
            public string ResponseBody { get; set; }
        }

        [Fact]
        public void ResolveTokens_SingleLevelProperty_StillWorks()
        {
            var vars = new Dictionary<string, object> { ["output"] = new Response { StatusCode = 200 } };

            Assert.Equal("200 = 200", VariablePathResolver.ResolveTokens("%v:output.StatusCode% = 200", vars));
        }

        [Fact]
        public void ResolveTokens_PathIntoJsonStringBody_ResolvesNestedField()
        {
            var vars = new Dictionary<string, object>
            {
                ["output"] = new Response { ResponseBody = "{\"results\":[{\"name\":\"Longsword\"},{\"name\":\"Dagger\"}]}" }
            };

            Assert.Equal("Dagger", VariablePathResolver.ResolveTokens("%v:output.responseBody.results[1].name%", vars));
        }

        [Fact]
        public void ResolveTokens_ObjectResult_IsSerializedAsCompactJson()
        {
            var vars = new Dictionary<string, object> { ["data"] = JObject.Parse("{\"a\":{\"b\":1}}") };

            Assert.Equal("{\"b\":1}", VariablePathResolver.ResolveTokens("%v:data.a%", vars));
        }

        [Fact]
        public void ResolveTokens_ListIndexAndDictionary_Resolve()
        {
            var vars = new Dictionary<string, object>
            {
                ["items"] = new List<object> { new Dictionary<string, object> { ["Hp"] = 7 } }
            };

            Assert.Equal("7", VariablePathResolver.ResolveTokens("%v:items[0].hp%", vars));
        }

        [Fact]
        public void ResolveTokens_MissingSegment_ResolvesToEmpty()
        {
            var vars = new Dictionary<string, object> { ["output"] = new Response() };

            Assert.Equal("[]", VariablePathResolver.ResolveTokens("[%v:output.Nope.deeper%]", vars));
            Assert.Equal("[]", VariablePathResolver.ResolveTokens("[%v:missing.x%]", vars));
        }

        [Fact]
        public void Prepare_DtoPrefix_SerializesVariableAsCamelCaseJson()
        {
            var vars = new Dictionary<string, object>
            {
                ["ids"] = new List<string> { "a", "b" },
                ["resp"] = new Response { StatusCode = 404 },
                ["name"] = "Bob",
            };

            Assert.Equal("[\"a\",\"b\"]", "%dto:ids%".Prepare(vars));
            Assert.Equal("{\"statusCode\":404,\"responseBody\":null}", "%dto:resp%".Prepare(vars));
            Assert.Equal("{\"n\": \"Bob\"}", "{\"n\": %dto:name%}".Prepare(vars));
        }

        [Fact]
        public void Prepare_UnknownVariable_LeftUntouched()
        {
            var vars = new Dictionary<string, object> { ["x"] = 1 };

            Assert.Equal("%y% %dto:y% 1", "%y% %dto:y% %x%".Prepare(vars));
        }
    }
}
