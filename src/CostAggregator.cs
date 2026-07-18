namespace ClaudeCostWatch;

sealed class CostAggregator
{
    private readonly object _lock = new();
    private decimal _daily;
    private decimal _monthly;
    private bool _hasData;
    private Dictionary<string, (decimal Daily, decimal Monthly)> _projects = new();

    public void Add(decimal cost, bool isToday, string project)
    {
        lock (_lock)
        {
            _monthly += cost;
            if (isToday) _daily += cost;
            _hasData = true;

            _projects.TryGetValue(project, out var p);
            _projects[project] = (p.Daily + (isToday ? cost : 0), p.Monthly + cost);
        }
    }

    public void Reset(decimal daily, decimal monthly, bool hasData,
        Dictionary<string, (decimal Daily, decimal Monthly)> projects)
    {
        lock (_lock)
        {
            _daily = daily;
            _monthly = monthly;
            _hasData = hasData;
            _projects = projects;
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

    public IReadOnlyDictionary<string, (decimal Daily, decimal Monthly)> GetProjectTotals()
    {
        lock (_lock)
        {
            return new Dictionary<string, (decimal Daily, decimal Monthly)>(_projects);
        }
    }
}
