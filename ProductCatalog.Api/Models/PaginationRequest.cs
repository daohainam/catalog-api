using System.ComponentModel;

namespace ProductCatalog.Api.Models
{
    public class PaginationRequest(int pageSize = 20, int pageIndex = 0)
    {
        [property: DefaultValue(20)]
        public int PageSize { get; set; } = pageSize;


        [property: DefaultValue(0)]
        public int PageIndex { get; set; } = pageIndex;
    }
}
