using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ClaudeCostWatch;

static class LogParser
{
    // Each Anthropic API response is written as multiple log entries (one per content block)
    // all sharing the same requestId. We deduplicate by requestId so tokens are counted once.
    public static IEnumerable<UsageEntry> Parse(string filePath, long startOffset = 0)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(startOffset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var entry = TryParseLine(line, seen);
            if (entry != null)
                yield return entry;
        }
    }

    private static UsageEntry? TryParseLine(string line, HashSet<string> seen)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "assistant")
                return null;
            if (!root.TryGetProperty("requestId", out var reqProp))
                return null;

            var requestId = reqProp.GetString();
            if (requestId is null || !seen.Add(requestId))
                return null;

            if (!root.TryGetProperty("timestamp", out var tsProp))
                return null;
            if (!root.TryGetProperty("message", out var msg))
                return null;
            if (!msg.TryGetProperty("model", out var modelProp))
                return null;
            if (!msg.TryGetProperty("usage", out var usage))
                return null;

            // Split cache creation tokens into 5m and 1h buckets using the nested breakdown.
            // Fall back to treating all as 5m if the breakdown is absent (older log entries).
            long cw5m, cw1h;
            if (usage.TryGetProperty("cache_creation", out var cc))
            {
                cw5m = GetLong(cc, "ephemeral_5m_input_tokens");
                cw1h = GetLong(cc, "ephemeral_1h_input_tokens");
            }
            else
            {
                cw5m = GetLong(usage, "cache_creation_input_tokens");
                cw1h = 0;
            }

            return new UsageEntry(
                Model: modelProp.GetString() ?? "",
                Timestamp: tsProp.GetDateTime(),
                InputTokens: GetLong(usage, "input_tokens"),
                OutputTokens: GetLong(usage, "output_tokens"),
                CacheCreation5mTokens: cw5m,
                CacheCreation1hTokens: cw1h,
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
