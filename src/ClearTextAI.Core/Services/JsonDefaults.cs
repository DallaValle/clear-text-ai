using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClearTextAI.Core.Services;

internal static class JsonDefaults
{
    /// <summary>Enums are written as names so a settings or history file stays readable and survives reordering.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
