using AutoMapper;
using ProductCatalog.Api.Models;

namespace ProductCatalog.Api
{
    public class ModelProfile: Profile
    {
        public ModelProfile()
        {
            CreateMap<Category, ProductCatalog.Infrastructure.Entities.Category>();
            CreateMap<Brand, ProductCatalog.Infrastructure.Entities.Brand>();
            CreateMap<Product, ProductCatalog.Infrastructure.Entities.Product>();
        }
    }
}
