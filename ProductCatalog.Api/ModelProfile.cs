using AutoMapper;
using ProductCatalog.Api.Models;

namespace ProductCatalog.Api
{
    public class ModelProfile: Profile
    {
        public ModelProfile()
        {
            CreateMap<Category, Infrastructure.Entities.Category>();
            CreateMap<Brand, Infrastructure.Entities.Brand>();
            CreateMap<Product, Infrastructure.Entities.Product>();
            CreateMap<Dimension, Infrastructure.Entities.Dimension>();

            CreateMap<Infrastructure.Entities.Dimension, Dimension>();
        }
    }
}
