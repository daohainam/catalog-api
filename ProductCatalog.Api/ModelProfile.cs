using AutoMapper;
using ProductCatalog.Api.Models;

namespace ProductCatalog.Api
{
    public class ModelProfile: Profile
    {
        public ModelProfile()
        {
            CreateMap<Category, ProductCatalog.Infrastructure.Entities.Category>();
            CreateMap<Branch, ProductCatalog.Infrastructure.Entities.Branch>();
            CreateMap<Product, ProductCatalog.Infrastructure.Entities.Product>();
        }
    }
}
