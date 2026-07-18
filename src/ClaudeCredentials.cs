using System.IO;
using System.Text.Json;

namespace ClaudeCostWatch;

sealed class ClaudeCredentials
{
    private static readonly string CredentialsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", ".credentials.json");

    // null = API key auth (env var) or credentials file absent/unreadable
    public string? SubscriptionType { get; private init; }

    public bool IsSubscription => SubscriptionType is not null;

    // "Pro", "Max", or "API"
    public string PlanLabel => SubscriptionType is null
        ? "API"
        : char.ToUpper(SubscriptionType[0]) + SubscriptionType[1..].ToLower();

    public static ClaudeCredentials Load()
    {
        try
        {
            if (!File.Exists(CredentialsPath)) return new();

            using var doc = JsonDocument.Parse(File.ReadAllText(CredentialsPath));
            if (doc.RootElement.TryGetProperty("claudeAiOauth", out var oauth) &&
                oauth.TryGetProperty("subscriptionType", out var sub) &&
                sub.GetString() is string subType)
            {
                return new() { SubscriptionType = subType };
            }
        }
        catch { }

        return new();
    }
}
