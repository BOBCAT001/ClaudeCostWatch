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
            _aggregator.Reset(0, 0, false);
            return Task.CompletedTask;
        }

        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        decimal daily = 0, monthly = 0;
        bool hasData = false;

        foreach (var file in Directory.EnumerateFiles(_logRoot, "*.jsonl", SearchOption.AllDirectories))
        {
            foreach (var entry in LogParser.Parse(file))
            {
                var cost = _calculator.Calculate(entry);
                if (cost is null) continue;

                hasData = true;
                var local = entry.Timestamp.ToLocalTime();
                if (local >= monthStart) monthly += cost.Value;
                if (local.Date == today) daily += cost.Value;
            }

            _fileOffsets[file] = new FileInfo(file).Length;
        }

        _aggregator.Reset(daily, monthly, hasData);
        return Task.CompletedTask;
    }

    public void OpenLogFolder() =>
        Process.Start(new ProcessStartInfo("explorer.exe", _logRoot) { UseShellExecute = true });

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (!File.Exists(e.FullPath)) return;

            var offset = _fileOffsets.TryGetValue(e.FullPath, out var o) ? o : 0;
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);

            foreach (var entry in LogParser.Parse(e.FullPath, offset))
            {
                var cost = _calculator.Calculate(entry);
                if (cost is null) continue;

                var local = entry.Timestamp.ToLocalTime();
                if (local >= monthStart)
                    _aggregator.Add(cost.Value, isToday: local.Date == today);
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
