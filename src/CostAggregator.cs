namespace ClaudeCostWatch;

sealed class CostAggregator
{
    private readonly object _lock = new();
    private decimal _daily;
    private decimal _weekly;
    private decimal _monthly;
    private bool _hasData;
    private Dictionary<string, (decimal Daily, decimal Weekly, decimal Monthly)> _projects = new();
    private HashSet<string> _unknownModels = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _seenModels = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<DateOnly, decimal> _dailyCosts = new();
    private Dictionary<DateOnly, Dictionary<string, decimal>> _dayProjectCosts = new();

    public void AddSeenModel(string model)
    {
        lock (_lock)
            _seenModels.Add(model);
    }

    public void AddUnknownModel(string model)
    {
        lock (_lock)
            _unknownModels.Add(model);
    }

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

    public void AddHistoricalEntry(DateOnly date, string project, decimal cost)
    {
        lock (_lock)
        {
            _dailyCosts[date] = _dailyCosts.TryGetValue(date, out var d) ? d + cost : cost;
            if (!_dayProjectCosts.TryGetValue(date, out var dp))
                _dayProjectCosts[date] = dp = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            dp[project] = dp.TryGetValue(project, out var pc) ? pc + cost : cost;
        }
    }

    public void Reset(decimal daily, decimal weekly, decimal monthly, bool hasData,
        Dictionary<string, (decimal Daily, decimal Weekly, decimal Monthly)> projects,
        HashSet<string> unknownModels,
        HashSet<string> seenModels,
        Dictionary<DateOnly, decimal> dailyCosts,
        Dictionary<DateOnly, Dictionary<string, decimal>> dayProjectCosts)
    {
        lock (_lock)
        {
            _daily = daily;
            _weekly = weekly;
            _monthly = monthly;
            _hasData = hasData;
            _projects = projects;
            _unknownModels = unknownModels;
            _seenModels = seenModels;
            _dailyCosts = dailyCosts;
            _dayProjectCosts = dayProjectCosts;
        }
    }

    public IReadOnlyCollection<string> GetSeenModels()
    {
        lock (_lock)
            return [.. _seenModels];
    }

    public IReadOnlyCollection<string> GetUnknownModels()
    {
        lock (_lock)
            return [.. _unknownModels];
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

    public (IReadOnlyDictionary<DateOnly, decimal> DailyCosts, IReadOnlyDictionary<DateOnly, IReadOnlyDictionary<string, decimal>> DayProjectCosts) GetHistoricalData()
    {
        lock (_lock)
        {
            var daily = new Dictionary<DateOnly, decimal>(_dailyCosts);
            var dayProject = _dayProjectCosts.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyDictionary<string, decimal>)new Dictionary<string, decimal>(kvp.Value, StringComparer.OrdinalIgnoreCase));
            return (daily, dayProject);
        }
    }
}
