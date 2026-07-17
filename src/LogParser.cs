using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ClaudeCostWatch;

static class LogParser
{
    public static IEnumerable<UsageEntry> Parse(string filePath, long startOffset = 0)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(startOffset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var entry = TryParseLine(line);
            if (entry != null)
                yield return entry;
        }
    }

    private static UsageEntry? TryParseLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "assistant")
                return null;
            if (!root.TryGetProperty("timestamp", out var tsProp))
                return null;
            if (!root.TryGetProperty("message", out var msg))
                return null;
            if (!msg.TryGetProperty("model", out var modelProp))
                return null;
            if (!msg.TryGetProperty("usage", out var usage))
                return null;

            return new UsageEntry(
                Model: modelProp.GetString() ?? "",
                Timestamp: tsProp.GetDateTime(),
                InputTokens: GetLong(usage, "input_tokens"),
                OutputTokens: GetLong(usage, "output_tokens"),
                CacheCreationTokens: GetLong(usage, "cache_creation_input_tokens"),
                CacheReadTokens: GetLong(usage, "cache_read_input_tokens"));
        }
        catch
        {
            return null;
        }
    }

    private static long GetLong(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) ? prop.GetInt64() : 0;
}
