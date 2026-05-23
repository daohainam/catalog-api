namespace ProductCatalog.Api.Apis;

public static class GroupApi
{
    public static RouteGroupBuilder MapGroupApi(this RouteGroupBuilder group)
    {
        var groupApiGroup = group.MapGroup("groups").WithTags("Group");
        groupApiGroup.MapPost("/", CreateGroup);

        return group;
    }

    private static async Task<Results<Ok<Group>, BadRequest, BadRequest<string>>> CreateGroup([AsParameters] ApiServices services, Group group)
    {
        if (group == null)
        {
            return TypedResults.BadRequest();
        }
        if (string.IsNullOrEmpty(group.Name))
        {
            return TypedResults.BadRequest("Group Name is required.");
        }

        if (group.Id == Guid.Empty)
            group.Id = Guid.CreateVersion7();

        await services.DbContext.Groups.AddAsync(group);
        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok(group);
    }
}
