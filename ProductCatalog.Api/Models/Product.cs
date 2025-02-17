using Microsoft.EntityFrameworkCore;

namespace ProductCatalog.Api.Models
{
    public record ProductCreate(
            string Name,
            string Description,
            double PriceFrom,
            double PriceTo,
            Guid CategoryId,
            string CategoryName,
            Guid BranchId,
            string BranchName,
            string[] Images
        );

    public record Product(
                Guid Id,
                string Name,
                string Description,
                double PriceFrom,
                double PriceTo,
                Guid CategoryId,
                string CategoryName,
                Guid BranchId,
                string BranchName,
                string[] Images
            ) : ProductCreate(Name, Description, PriceFrom, PriceTo, CategoryId, CategoryName, BranchId, BranchName, Images);

}
