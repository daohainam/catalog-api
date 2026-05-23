namespace ProductCatalog.Api.Apis;

public static class PaginationDefaults
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
}

public static class PaginationHelper
{
    public static (int offset, int limit) ValidatePagination(int? offset, int? limit)
    {
        var validatedOffset = Math.Max(offset ?? 0, 0);
        var validatedLimit = Math.Min(Math.Max(limit ?? PaginationDefaults.DefaultPageSize, 1), PaginationDefaults.MaxPageSize);
        return (validatedOffset, validatedLimit);
    }
}
