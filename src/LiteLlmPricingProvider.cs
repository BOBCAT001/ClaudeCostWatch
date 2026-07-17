using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace ClaudeCostWatch;

record ModelRates(decimal InputPerToken, decimal OutputPerToken, decimal CacheWritePerToken, decimal CacheReadPerToken);

sealed class LiteLlmPricingProvider
{
    private const string PricingUrl =
        "https://raw.githubusercontent.com/BerriAI/litellm/main/model_prices_and_context_window.json";

    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClaudeCostWatch", "model_prices.json");

    private Dictionary<string, ModelRates> _rates = new(StringComparer.OrdinalIgnoreCase);

    public async Task LoadAsync()
    {
        bool cacheStale = !File.Exists(CachePath)
            || File.GetLastWriteTime(CachePath) < DateTime.Now.AddHours(-24);

        if (cacheStale)
            await FetchAndCacheAsync();

        if (File.Exists(CachePath))
            ParseFile(await File.ReadAllTextAsync(CachePath));
    }

    public async Task RefreshAsync()
    {
        await FetchAndCacheAsync();
        if (File.Exists(CachePath))
            ParseFile(await File.ReadAllTextAsync(CachePath));
    }

    public ModelRates? GetRates(string model) =>
        _rates.TryGetValue(model, out var rates) ? rates : null;

    private async Task FetchAndCacheAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var json = await http.GetStringAsync(PricingUrl);
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            await File.WriteAllTextAsync(CachePath, json);
        }
        catch
        {
            // Use stale cache or remain empty until next attempt
        }
    }

    private void ParseFile(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, ModelRates>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var el = prop.Value;
                if (!TryGetDecimal(el, "input_cost_per_token", out var input)) continue;
                if (!TryGetDecimal(el, "output_cost_per_token", out var output)) continue;
                if (!TryGetDecimal(el, "cache_creation_input_token_cost", out var cacheWrite)) continue;
                if (!TryGetDecimal(el, "cache_read_input_token_cost", out var cacheRead)) continue;

                result[prop.Name] = new ModelRates(input, output, cacheWrite, cacheRead);
            }

            _rates = result;
        }
        catch
        {
            // Malformed file — leave existing rates in place
        }
    }

    private static bool TryGetDecimal(JsonElement el, string name, out decimal value)
    {
        value = 0;
        return el.TryGetProperty(name, out var prop) && prop.TryGetDecimal(out value);
    }
}
