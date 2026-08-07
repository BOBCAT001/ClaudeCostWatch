using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeCostWatch;

sealed class ModelRateOverride
{
    [JsonPropertyName("inputPerMillion")]
    public decimal InputPerMillion { get; set; }

    [JsonPropertyName("outputPerMillion")]
    public decimal OutputPerMillion { get; set; }

    [JsonPropertyName("cacheWrite5mPerMillion")]
    public decimal CacheWrite5mPerMillion { get; set; }

    [JsonPropertyName("cacheWrite1hPerMillion")]
    public decimal CacheWrite1hPerMillion { get; set; }

    [JsonPropertyName("cacheReadPerMillion")]
    public decimal CacheReadPerMillion { get; set; }

    public ModelRates ToModelRates() => new(
        InputPerMillion / 1_000_000m,
        OutputPerMillion / 1_000_000m,
        CacheWrite5mPerMillion / 1_000_000m,
        (CacheWrite1hPerMillion == 0 ? CacheWrite5mPerMillion : CacheWrite1hPerMillion) / 1_000_000m,
        CacheReadPerMillion / 1_000_000m);
}

sealed class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClaudeCostWatch", "settings.json");

    [JsonPropertyName("logFolder")]
    public string? LogFolder { get; set; }

    [JsonPropertyName("dailyThreshold")]
    public decimal? DailyThreshold { get; set; }

    [JsonPropertyName("modelRateOverrides")]
    public Dictionary<string, ModelRateOverride> ModelRateOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
