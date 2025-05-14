using DndOnePlaceManager.Domain.Entities.BattleMap;
using Newtonsoft.Json.Linq;

namespace DndOnePlaceManager.Application.Helpers
{
    internal class DetailsParseHelper
    {
        public static Dictionary<string,ElementDetailModel> ParseFabricJSToElementDetals(Guid? elementId, string json)
        {
            var details = new Dictionary<string, ElementDetailModel>();
            var jObject = Newtonsoft.Json.Linq.JObject.Parse(json);
            foreach (var item in jObject)
            {
                if(item.Value.Type == JTokenType.Null || item.Value.Type == JTokenType.Undefined)
                {
                    details.Add(item.Key, new ElementDetailModel()
                    {
                        Key = item.Key,
                        Value = null,
                        Type = item.Value.Type.ToString(),
                        ElementId = elementId ?? Guid.Empty,
                    });
                }
                else
                {
                    details.Add(item.Key, new ElementDetailModel()
                    {
                        Key = item.Key,
                        Value = item.Value.ToString(),
                        Type = item.Value.Type.ToString(),
                        ElementId = elementId ?? Guid.Empty,
                    });
                }
            }
            return details;
        }

        public static string ParseElementDetailsToFabricJS(List<ElementDetailModel> details)
        {
            var jObject = new Newtonsoft.Json.Linq.JObject();
            foreach (var item in details)
            {
                //Handle type
                switch (item.Type)
                {
                    case "String":
                        jObject.Add(item.Key, item.Value);
                        break;
                    case "Integer":
                    case "Float":
                    case "Number":
                        jObject.Add(item.Key, float.Parse(item.Value));
                        break;
                    case "Boolean":
                        jObject.Add(item.Key, bool.Parse(item.Value));
                        break;
                    case "Object":
                        jObject.Add(item.Key, Newtonsoft.Json.Linq.JObject.Parse(item.Value));
                        break;
                    case "Array":
                        jObject.Add(item.Key, Newtonsoft.Json.Linq.JArray.Parse(item.Value));
                        break;
                    default:
                        jObject.Add(item.Key, item.Value);
                        break;
                }
            }
            return jObject.ToString();
        }

        internal static object ParseValueType(string value, string type)
        {
            switch (type)
            {
                case "String":
                    return value;
                case "Integer":
                case "Float":
                case "Number":
                    return float.Parse(value);
                case "Boolean":
                    return bool.Parse(value);
                case "Object":
                    return Newtonsoft.Json.Linq.JObject.Parse(value);
                case "Array":
                    return Newtonsoft.Json.Linq.JArray.Parse(value);
                default:
                    return value;
            }
        }
    }
}
