namespace ClaudeCostWatch;

sealed class CostCalculator(LiteLlmPricingProvider pricing)
{
    public decimal? Calculate(UsageEntry entry)
    {
        var rates = pricing.GetRates(entry.Model);
        if (rates is null)
            return null;

        return (entry.InputTokens * rates.InputPerToken)
             + (entry.OutputTokens * rates.OutputPerToken)
             + (entry.CacheCreationTokens * rates.CacheWritePerToken)
             + (entry.CacheReadTokens * rates.CacheReadPerToken);
    }
}
