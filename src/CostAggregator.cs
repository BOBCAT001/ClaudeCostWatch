namespace ClaudeCostWatch;

sealed class CostAggregator
{
    private readonly object _lock = new();
    private decimal _daily;
    private decimal _monthly;
    private bool _hasData;

    public void Add(decimal cost, bool isToday)
    {
        lock (_lock)
        {
            _monthly += cost;
            if (isToday) _daily += cost;
            _hasData = true;
        }
    }

    public void Reset(decimal daily, decimal monthly, bool hasData)
    {
        lock (_lock)
        {
            _daily = daily;
            _monthly = monthly;
            _hasData = hasData;
        }
    }

    // Returns null when no pricing data was available during the last scan
    public (decimal? Daily, decimal? Monthly) GetTotals()
    {
        lock (_lock)
        {
            if (!_hasData) return (null, null);
            return (_daily, _monthly);
        }
    }
}
