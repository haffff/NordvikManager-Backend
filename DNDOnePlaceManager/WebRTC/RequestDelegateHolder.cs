using Microsoft.AspNetCore.Http;

namespace DNDOnePlaceManager.WebRTC
{
    public class RequestDelegateHolder
    {
        public RequestDelegate? Pipeline { get; set; }
    }
}
