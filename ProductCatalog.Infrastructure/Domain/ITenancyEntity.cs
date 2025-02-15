namespace ProductCatalog.Api.Infrastructure.Domain
{
    public interface ITenancyEntity
    {
        Guid TenantId { get; set; }
    }
}
