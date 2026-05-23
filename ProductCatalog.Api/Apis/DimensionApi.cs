namespace ProductCatalog.Api.Apis;

public static class DimensionApi
{
    private static readonly string InvalidDisplayType = $"Invalid display type. Valid types are: {string.Join(", ", DimensionDisplayTypes.All)}";

    public static RouteGroupBuilder MapDimensionApi(this RouteGroupBuilder group)
    {
        var dimensionApiGroup = group.MapGroup("dimensions").WithTags("Dimension");
        dimensionApiGroup.MapPost("/", CreateDimensions);
        dimensionApiGroup.MapPost("/{id}/values", AddDimensionValues);
        dimensionApiGroup.MapGet("/", async ([AsParameters] ApiServices services, [FromQuery] int? offset = 0, [FromQuery] int? limit = PaginationDefaults.DefaultPageSize) =>
        {
            var (validatedOffset, validatedLimit) = PaginationHelper.ValidatePagination(offset, limit);
            return await services.DbContext.Dimensions
            .AsNoTracking()
            .Include(d => d.Values)
            .OrderBy(d => d.Id)
            .Skip(validatedOffset).Take(validatedLimit)
            .ToListAsync(services.CancellationToken);
        });

        return group;
    }

    private static async Task<Results<Ok<DimensionValue[]>, BadRequest, BadRequest<string>>> AddDimensionValues([AsParameters] ApiServices services, [FromRoute] string id, DimensionValue[] dimensionValues)
    {
        if (dimensionValues == null || dimensionValues.Length == 0)
        {
            return TypedResults.BadRequest();
        }

        foreach (var dimensionValue in dimensionValues)
        {
            if (dimensionValue.Id == Guid.Empty)
                dimensionValue.Id = Guid.CreateVersion7();

            dimensionValue.DimensionId = id;
        }
        await services.DbContext.DimensionValues.AddRangeAsync(dimensionValues, services.CancellationToken);
        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok(dimensionValues);
    }

    private static async Task<Results<Ok<Dimension[]>, BadRequest, BadRequest<string>>> CreateDimensions([AsParameters] ApiServices services, Dimension[] dimensions)
    {
        if (dimensions == null || dimensions.Length == 0)
        {
            return TypedResults.BadRequest();
        }

        foreach (var dimension in dimensions)
        {
            if (string.IsNullOrEmpty(dimension.Id))
                return TypedResults.BadRequest("Dimension Id is required.");
            if (string.IsNullOrEmpty(dimension.DisplayType))
                dimension.DisplayType = DimensionDisplayTypes.Text;
            else if (!DimensionDisplayTypes.Has(dimension.DisplayType))
                return TypedResults.BadRequest(InvalidDisplayType);

            if (!IsValidDimensionId(dimension.Id))
            {
                return TypedResults.BadRequest("Dimension Id can only contain alphanumeric characters and underscores.");
            }

            dimension.DefaultValue ??= "";
            await services.DbContext.Dimensions.AddAsync(dimension);

            if (dimension.Values != null && dimension.Values.Count > 0)
            {
                foreach (var value in dimension.Values)
                {
                    if (value.Id == Guid.Empty)
                        value.Id = Guid.CreateVersion7();
                    value.DimensionId = dimension.Id;
                }
                await services.DbContext.DimensionValues.AddRangeAsync(dimension.Values);
            }
        }

        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok(dimensions);
    }

    private static bool IsValidDimensionId(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        if (!(id[0] >= 'a' && id[0] <= 'z')) // must start with a lowercase letter
        {
            return false;
        }

        return id.All(c => c == '_' || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')); // only allow lowercase letters, numbers and underscores
    }
}
