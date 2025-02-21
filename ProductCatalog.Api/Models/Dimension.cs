namespace ProductCatalog.Api.Models;

public record DimensionCreate(DimensionDisplayTypes DisplayType, string Name, string DisplayName, List<string> Values);
public record Dimension(Guid Id, Guid ProductId, DimensionDisplayTypes DisplayType, string Name, string DisplayName, List<string> Values) : DimensionCreate(DisplayType, Name, DisplayName, Values);
public record class DimensionValue(string Name, string Value);

public enum DimensionDisplayTypes
{
    Color,
    Text
}
