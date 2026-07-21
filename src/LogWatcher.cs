using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

namespace ClaudeCostWatch;

sealed class LogWatcher : IDisposable
{
    private readonly string _logRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    private readonly CostCalculator _calculator;
    private readonly CostAggregator _aggregator;
    private readonly ConcurrentDictionary<string, long> _fileOffsets = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _fsw;

    public LogWatcher(CostCalculator calculator, CostAggregator aggregator)
    {
        _calculator = calculator;
        _aggregator = aggregator;
    }

    public async Task StartAsync()
    {
        await RescanAsync();

        if (!Directory.Exists(_logRoot)) return;

        _fsw = new FileSystemWatcher(_logRoot, "*.jsonl")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _fsw.Changed += OnFileChanged;
        _fsw.Created += OnFileChanged;
    }

    public Task RescanAsync()
    {
        _fileOffsets.Clear();

        if (!Directory.Exists(_logRoot))
        {
            _aggregator.Reset(0, 0, 0, false, new(), new());
            return Task.CompletedTask;
        }

        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // Monday

        decimal daily = 0, weekly = 0, monthly = 0;
        bool hasData = false;
        var projects = new Dictionary<string, (decimal Daily, decimal Weekly, decimal Monthly)>(StringComparer.OrdinalIgnoreCase);
        var unknownModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(_logRoot, "*.jsonl", SearchOption.AllDirectories))
        {
            var project = Path.GetFileName(Path.GetDirectoryName(file)!) ?? "unknown";

            foreach (var entry in LogParser.Parse(file))
            {
                var cost = _calculator.Calculate(entry);
                if (cost is null) { unknownModels.Add(entry.Model); continue; }

                hasData = true;
                var local = entry.Timestamp.ToLocalTime();
                bool thisMonth = local >= monthStart;
                bool thisWeek = local.Date >= weekStart;
                bool thisDay = local.Date == today;

                if (thisMonth) monthly += cost.Value;
                if (thisWeek) weekly += cost.Value;
                if (thisDay) daily += cost.Value;

                if (thisMonth || thisWeek || thisDay)
                {
                    projects.TryGetValue(project, out var p);
                    projects[project] = (
                        p.Daily + (thisDay ? cost.Value : 0),
                        p.Weekly + (thisWeek ? cost.Value : 0),
                        p.Monthly + (thisMonth ? cost.Value : 0));
                }
            }

            _fileOffsets[file] = new FileInfo(file).Length;
        }

        _aggregator.Reset(daily, weekly, monthly, hasData, projects, unknownModels);
        return Task.CompletedTask;
    }

    public void OpenLogFolder() =>
        Process.Start(new ProcessStartInfo("explorer.exe", _logRoot) { UseShellExecute = true });

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (!File.Exists(e.FullPath)) return;

            var project = Path.GetFileName(Path.GetDirectoryName(e.FullPath)!) ?? "unknown";
            var offset = _fileOffsets.TryGetValue(e.FullPath, out var o) ? o : 0;
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));

            foreach (var entry in LogParser.Parse(e.FullPath, offset))
            {
                var cost = _calculator.Calculate(entry);
                if (cost is null) { _aggregator.AddUnknownModel(entry.Model); continue; }

                var local = entry.Timestamp.ToLocalTime();
                if (local >= monthStart)
                    _aggregator.Add(cost.Value,
                        isToday: local.Date == today,
                        isThisWeek: local.Date >= weekStart,
                        project: project);
            }

            _fileOffsets[e.FullPath] = new FileInfo(e.FullPath).Length;
        }
        catch
        {
            // File access errors (locked, deleted) are transient — the 30s rescan will catch up
        }
    }

    public void Dispose() => _fsw?.Dispose();
}
