namespace ProductCatalog.Api.Apis;

public static class CategoryApi
{
    public static RouteGroupBuilder MapCategoryApi(this RouteGroupBuilder group)
    {
        var categoryApiGroup = group.MapGroup("categories").WithTags("Category");
        categoryApiGroup.MapPost("/", CreateCategory);

        return group;
    }

    private static async Task<Results<Ok<Category>, BadRequest, BadRequest<string>>> CreateCategory([AsParameters] ApiServices services, Category category)
    {
        if (category == null)
        {
            return TypedResults.BadRequest();
        }

        if (string.IsNullOrEmpty(category.Name))
        {
            return TypedResults.BadRequest("Category Name is required.");
        }

        if (string.IsNullOrEmpty(category.UrlSlug))
        {
            return TypedResults.BadRequest("Category UrlSlug is required.");
        }

        if (category.Id == Guid.Empty)
            category.Id = Guid.CreateVersion7();

        await services.DbContext.Categories.AddAsync(category);
        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok(category);
    }
}
