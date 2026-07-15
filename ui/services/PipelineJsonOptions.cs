using System.Text.Json;
using System.Text.Json.Serialization;

namespace LimelightX.UI.Services;

/// <summary>
/// Shared (de)serialization options for the /src/api wire contract
/// (ui-data-contracts.md, api.md). snake_case on the wire maps to PascalCase
/// C# properties; severity/category enum strings deserialize case-insensitively
/// per ui-data-contracts.md §1's "Rules" - both requirements are satisfied by
/// this single options instance with zero custom converters.
/// </summary>
public static class PipelineJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false) },
    };
}
