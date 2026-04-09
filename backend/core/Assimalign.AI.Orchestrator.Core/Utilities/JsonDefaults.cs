using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assimalign.AI.Orchestrator.Core.Utilities;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    static JsonDefaults()
    {
        Options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }
}
