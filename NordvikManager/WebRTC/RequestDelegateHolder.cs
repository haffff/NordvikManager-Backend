using Microsoft.AspNetCore.Http;

namespace NordvikManager.WebRTC
{
    public class RequestDelegateHolder
    {
        public RequestDelegate? Pipeline { get; set; }
    }
}
