using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using System;

namespace DNDOnePlaceManager.Extensions
{
    public static class JExtensions
    {
        public static Guid ToGuid(this JToken token)
        {
            Guid.TryParse(token?.ToString(), out var value);
            return value;
        }
    }
}
