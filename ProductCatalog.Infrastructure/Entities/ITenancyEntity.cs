namespace ProductCatalog.Infrastructure.Entities
{
    public interface ITenancyEntity
    {
        Guid TenantId { get; set; }
    }
}
