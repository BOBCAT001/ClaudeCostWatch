namespace ClaudeCostWatch;

sealed class CostAggregator
{
    private readonly object _lock = new();
    private decimal _daily;
    private decimal _weekly;
    private decimal _monthly;
    private bool _hasData;
    private Dictionary<string, (decimal Daily, decimal Weekly, decimal Monthly)> _projects = new();

    public void Add(decimal cost, bool isToday, bool isThisWeek, string project)
    {
        lock (_lock)
        {
            _monthly += cost;
            if (isThisWeek) _weekly += cost;
            if (isToday) _daily += cost;
            _hasData = true;

            _projects.TryGetValue(project, out var p);
            _projects[project] = (p.Daily + (isToday ? cost : 0), p.Weekly + (isThisWeek ? cost : 0), p.Monthly + cost);
        }
    }

    public void Reset(decimal daily, decimal weekly, decimal monthly, bool hasData,
        Dictionary<string, (decimal Daily, decimal Weekly, decimal Monthly)> projects)
    {
        lock (_lock)
        {
            _daily = daily;
            _weekly = weekly;
            _monthly = monthly;
            _hasData = hasData;
            _projects = projects;
        }
    }

    // Returns null when no pricing data was available during the last scan
    public (decimal? Daily, decimal? Weekly, decimal? Monthly) GetTotals()
    {
        lock (_lock)
        {
            if (!_hasData) return (null, null, null);
            return (_daily, _weekly, _monthly);
        }
    }

    public IReadOnlyDictionary<string, (decimal Daily, decimal Weekly, decimal Monthly)> GetProjectTotals()
    {
        lock (_lock)
        {
            return new Dictionary<string, (decimal Daily, decimal Weekly, decimal Monthly)>(_projects);
        }
    }
}
