namespace ClaudeCostWatch;

record UsageEntry(
    string Model,
    DateTime Timestamp,
    long InputTokens,
    long OutputTokens,
    long CacheCreationTokens,
    long CacheReadTokens);
