using AutoMapper;
using ProductCatalog.Api.Models;

namespace ProductCatalog.Api;
public class ModelProfile: Profile
{
    public ModelProfile()
    {
        CreateMap<CategoryCreate, Infrastructure.Entities.Category>()
            .ForMember(dst => dst.Id, opt => opt.MapFrom(src => Guid.CreateVersion7()));
        CreateMap<Category, Infrastructure.Entities.Category>();
        CreateMap<Infrastructure.Entities.Category, Category>();

        CreateMap<BrandCreate, Infrastructure.Entities.Brand>()
            .ForMember(dst => dst.Id, opt => opt.MapFrom(src => Guid.CreateVersion7()));
        CreateMap<Brand, Infrastructure.Entities.Brand>();
        CreateMap<Infrastructure.Entities.Brand, Brand>();

        CreateMap<ProductCreate, Infrastructure.Entities.Product>()
            .ForMember(dst => dst.Id, opt => opt.MapFrom(src => Guid.CreateVersion7()))
            .ForMember(dst => dst.IsPublished, opt => opt.MapFrom(src => Guid.CreateVersion7()))
            .ForMember(dst => dst.IsDeleted, opt => opt.MapFrom(src => Guid.CreateVersion7()))
            .ForMember(dst => dst.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dst => dst.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));
        CreateMap<Product, Infrastructure.Entities.Product>();
        CreateMap<Infrastructure.Entities.Product, Product>();

        CreateMap<DimensionCreate, Infrastructure.Entities.Dimension>()
            .ForMember(dst => dst.Id, opt => opt.MapFrom(src => Guid.CreateVersion7()))
            .ForMember(dst => dst.DimensionValues, opt => opt.MapFrom(src => src.Values.Select(v => new Infrastructure.Entities.DimensionValue
            {
                Id = Guid.CreateVersion7(),
                Value = v
            })));

        CreateMap<Dimension, Infrastructure.Entities.Dimension>();
        CreateMap<Infrastructure.Entities.Dimension, Dimension>();

        CreateMap<VariantCreate, Infrastructure.Entities.Variant>()
            .ForMember(dst => dst.Id, opt => opt.MapFrom(src => Guid.CreateVersion7()));
        CreateMap<Variant, Infrastructure.Entities.Variant>();
        CreateMap<Infrastructure.Entities.Variant, Variant>();

        CreateMap<DimensionValueCreate, Infrastructure.Entities.DimensionValue>()
            .ForMember(dst => dst.Id, opt => opt.MapFrom(src => Guid.CreateVersion7()));
    }
}
