using System.IO;
using System.Text;

namespace ClaudeCostWatch;

sealed class UsageLogger
{
    private string? _taskId;
    private DateTime _startTime;
    private string? _logFile;
    private long _entryStartOffset;
    private IReadOnlyDictionary<string, (decimal Daily, decimal Weekly, decimal Monthly)> _snapshot
        = new Dictionary<string, (decimal, decimal, decimal)>();

    public bool IsLogging => _taskId is not null;
    public string? ActiveTaskId => _taskId;

    public void Start(string taskId, string logFolder,
        IReadOnlyDictionary<string, (decimal Daily, decimal Weekly, decimal Monthly)> projects)
    {
        _taskId = taskId;
        _startTime = DateTime.Now;
        _snapshot = projects;

        Directory.CreateDirectory(logFolder);
        _logFile = Path.Combine(logFolder, "usage_log.md");
        _entryStartOffset = File.Exists(_logFile) ? new FileInfo(_logFile).Length : 0;

        WriteEntry(inProgress: true, stopTime: null, projects);
    }

    public void Update(IReadOnlyDictionary<string, (decimal Daily, decimal Weekly, decimal Monthly)> projects)
    {
        if (!IsLogging) return;
        WriteEntry(inProgress: true, stopTime: null, projects);
    }

    public void Stop(IReadOnlyDictionary<string, (decimal Daily, decimal Weekly, decimal Monthly)> projects)
    {
        if (!IsLogging) return;
        WriteEntry(inProgress: false, stopTime: DateTime.Now, projects);
        _taskId = null;
        _logFile = null;
    }

    private void WriteEntry(bool inProgress, DateTime? stopTime,
        IReadOnlyDictionary<string, (decimal Daily, decimal Weekly, decimal Monthly)> projects)
    {
        var sb = new StringBuilder();

        var timeRange = inProgress
            ? $"{_startTime:yyyy-MM-dd HH:mm} *(in progress)*"
            : $"{_startTime:yyyy-MM-dd HH:mm}–{stopTime!.Value:HH:mm}";

        sb.AppendLine($"## {_taskId} — {timeRange}");
        sb.AppendLine();
        sb.AppendLine("| Project | Cost |");
        sb.AppendLine("|---------|------|");

        decimal taskTotal = 0;
        foreach (var (encoded, current) in projects.OrderByDescending(p => p.Value.Monthly))
        {
            _snapshot.TryGetValue(encoded, out var snap);
            var delta = Math.Max(0m, current.Monthly - snap.Monthly);
            if (delta == 0m) continue;
            taskTotal += delta;
            sb.AppendLine($"| {ProjectNames.Decode(encoded)} | {delta:C2} |");
        }

        var totalRow = taskTotal == 0m
            ? "| *(no usage recorded yet)* | — |"
            : $"| **Task total** | **{taskTotal:C2}** |";
        sb.AppendLine(totalRow);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        try
        {
            using var fs = new FileStream(_logFile!, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            fs.SetLength(_entryStartOffset);
            fs.Seek(0, SeekOrigin.End);
            using var writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(sb.ToString());
        }
        catch { }
    }
}
