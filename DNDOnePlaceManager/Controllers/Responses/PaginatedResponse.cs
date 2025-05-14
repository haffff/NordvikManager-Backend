using System;
using System.Collections;

namespace DNDOnePlaceManager.Controllers.Responses
{
    public class PaginatedResponse
    {
        public PaginatedResponse(int page, int count, ICollection data, int total)
        {
            Page = page;
            Count = count;
            Data = data;
            Total = total;
        }

        public PaginatedResponse()
        {
        }

        public int Page { get; set; }
        public int Count { get; set; }
        public int PagesTotal => Total == 0 ? 1 : (int)Math.Ceiling((double)Total / Count);
        public int NumberOfViewed => Page == PagesTotal ? Total : Count * Page;
        public int Total { get; set; }
        public ICollection Data { get; set; }
    }
}
