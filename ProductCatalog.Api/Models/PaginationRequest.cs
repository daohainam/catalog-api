namespace ProductCatalog.Api.Models
{
    public class PaginationRequest(int pageIndex, int pageSize)
    {
        public int PageIndex => pageIndex;
        public int PageSize => pageSize;
    }
}
