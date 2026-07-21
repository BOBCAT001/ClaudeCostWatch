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

    public void Reset(decimal daily, decimal weekly, decimal monthly, bool hasData,
        Dictionary<string, (decimal Daily, decimal Weekly, decimal Monthly)> projects,
        HashSet<string> unknownModels,
        HashSet<string> seenModels)
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
}
