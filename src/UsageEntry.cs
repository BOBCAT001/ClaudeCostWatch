namespace ClaudeCostWatch;

record UsageEntry(
    string Model,
    DateTime Timestamp,
    long InputTokens,
    long OutputTokens,
    long CacheCreation5mTokens,
    long CacheCreation1hTokens,
    long CacheReadTokens);
