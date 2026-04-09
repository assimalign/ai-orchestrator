using System.Text.Json;

namespace Assimalign.AI.Orchestrator.Core.Utilities;

public static class JsonExtraction
{
    public static T ExtractJsonObject<T>(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException("The model response did not contain a JSON object.");
        }

        var json = text[start..(end + 1)];
        var value = JsonSerializer.Deserialize<T>(json, JsonDefaults.Options);
        return value ?? throw new InvalidOperationException("The model response JSON was empty.");
    }

    public static string Serialize(object value) =>
        JsonSerializer.Serialize(value, JsonDefaults.Options);
}
