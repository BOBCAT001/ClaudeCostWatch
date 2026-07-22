using System.Data;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace ClaudeCostWatch;

sealed class ReportsForm : Form
{
    private readonly CostAggregator _aggregator;
    private readonly ClaudeCredentials _credentials;
    private readonly AppSettings _settings;
    private readonly DataGridView _gridByDay;
    private readonly DataGridView _gridByWeek;
    private readonly DataGridView _gridProjectByDay;
    private readonly DataGridView _gridDayByProject;
    private readonly DataGridView _gridProjectTotals;
    private readonly DataGridView _gridDayByTask;
    private readonly DataTable _tableByDay = new();
    private readonly DataTable _tableByWeek = new();
    private readonly DataTable _tableProjectByDay = new();
    private readonly DataTable _tableDayByProject = new();
    private readonly DataTable _tableProjectTotals = new();
    private readonly DataTable _tableDayByTask = new();
    private readonly Label _footer;

    private record ColDef(string Name, int Width = 0, string? Format = null, bool RightAlign = false, bool Fill = false, bool IsWeekCol = false);

    public ReportsForm(CostAggregator aggregator, ClaudeCredentials credentials, AppSettings settings)
    {
        _aggregator = aggregator;
        _credentials = credentials;
        _settings = settings;

        Text = "Cost Reports";
        Size = new Size(720, 520);
        MinimumSize = new Size(500, 350);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        Icon = LoadIcon();

        _tableByDay.Columns.Add("Date", typeof(DateTime));
        _tableByDay.Columns.Add("Cost", typeof(decimal));

        _tableByWeek.Columns.Add("Week", typeof(DateTime));
        _tableByWeek.Columns.Add("Cost", typeof(decimal));

        _tableProjectByDay.Columns.Add("Project", typeof(string));
        _tableProjectByDay.Columns.Add("Date", typeof(DateTime));
        _tableProjectByDay.Columns.Add("Cost", typeof(decimal));

        _tableDayByProject.Columns.Add("Date", typeof(DateTime));
        _tableDayByProject.Columns.Add("Project", typeof(string));
        _tableDayByProject.Columns.Add("Cost", typeof(decimal));

        _tableProjectTotals.Columns.Add("Project", typeof(string));
        _tableProjectTotals.Columns.Add("Total Cost", typeof(decimal));

        _tableDayByTask.Columns.Add("Date", typeof(DateTime));
        _tableDayByTask.Columns.Add("Task", typeof(string));
        _tableDayByTask.Columns.Add("Cost", typeof(decimal));

        _gridByDay = MakeGrid(_tableByDay,
            new ColDef("Date", Width: 110, Format: "yyyy-MM-dd"),
            new ColDef("Cost", Width: 110, Format: "C2", RightAlign: true));

        _gridByWeek = MakeGrid(_tableByWeek,
            new ColDef("Week", Width: 210, IsWeekCol: true),
            new ColDef("Cost", Width: 110, Format: "C2", RightAlign: true));

        _gridProjectByDay = MakeGrid(_tableProjectByDay,
            new ColDef("Project", Fill: true),
            new ColDef("Date", Width: 110, Format: "yyyy-MM-dd"),
            new ColDef("Cost", Width: 110, Format: "C2", RightAlign: true));

        _gridDayByProject = MakeGrid(_tableDayByProject,
            new ColDef("Date", Width: 110, Format: "yyyy-MM-dd"),
            new ColDef("Project", Fill: true),
            new ColDef("Cost", Width: 110, Format: "C2", RightAlign: true));

        _gridProjectTotals = MakeGrid(_tableProjectTotals,
            new ColDef("Project", Fill: true),
            new ColDef("Total Cost", Width: 120, Format: "C2", RightAlign: true));

        _gridDayByTask = MakeGrid(_tableDayByTask,
            new ColDef("Date", Width: 110, Format: "yyyy-MM-dd"),
            new ColDef("Task", Fill: true),
            new ColDef("Cost", Width: 110, Format: "C2", RightAlign: true));

        _footer = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 6, 0),
            ForeColor = SystemColors.GrayText,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 8f),
        };

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(MakeTab("Cost by Day", _gridByDay));
        tabs.TabPages.Add(MakeTab("Cost by Week", _gridByWeek));
        tabs.TabPages.Add(MakeTab("Project by Day", _gridProjectByDay));
        tabs.TabPages.Add(MakeTab("Day by Project", _gridDayByProject));
        tabs.TabPages.Add(MakeTab("Project Totals", _gridProjectTotals));
        tabs.TabPages.Add(MakeTab("Day by Task", _gridDayByTask));

        Controls.Add(tabs);
        Controls.Add(_footer);
    }

    public void RefreshData()
    {
        var (dailyCosts, dayProjectCosts, dayTaskCosts) = _aggregator.GetHistoricalData();

        _tableByDay.Rows.Clear();
        foreach (var (date, cost) in dailyCosts.OrderByDescending(d => d.Key))
            _tableByDay.Rows.Add(date.ToDateTime(TimeOnly.MinValue), cost);

        _tableByWeek.Rows.Clear();
        foreach (var (week, cost) in dailyCosts
            .GroupBy(d => GetWeekStart(d.Key))
            .Select(g => (Week: g.Key, Cost: g.Sum(x => x.Value)))
            .OrderByDescending(x => x.Week))
        {
            _tableByWeek.Rows.Add(week.ToDateTime(TimeOnly.MinValue), cost);
        }

        _tableProjectByDay.Rows.Clear();
        foreach (var (date, project, cost) in dayProjectCosts
            .SelectMany(d => d.Value.Select(p => (Date: d.Key, Project: p.Key, Cost: p.Value)))
            .OrderBy(x => ProjectNames.Decode(x.Project)).ThenByDescending(x => x.Date))
        {
            _tableProjectByDay.Rows.Add(ProjectNames.Decode(project), date.ToDateTime(TimeOnly.MinValue), cost);
        }

        _tableDayByProject.Rows.Clear();
        foreach (var (date, project, cost) in dayProjectCosts
            .SelectMany(d => d.Value.Select(p => (Date: d.Key, Project: p.Key, Cost: p.Value)))
            .OrderByDescending(x => x.Date).ThenBy(x => ProjectNames.Decode(x.Project)))
        {
            _tableDayByProject.Rows.Add(date.ToDateTime(TimeOnly.MinValue), ProjectNames.Decode(project), cost);
        }

        _tableProjectTotals.Rows.Clear();
        foreach (var (project, total) in dayProjectCosts
            .SelectMany(d => d.Value.Select(p => (Project: p.Key, Cost: p.Value)))
            .GroupBy(x => x.Project, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Project: g.Key, Total: g.Sum(x => x.Cost)))
            .OrderByDescending(x => x.Total))
        {
            _tableProjectTotals.Rows.Add(ProjectNames.Decode(project), total);
        }

        _tableDayByTask.Rows.Clear();
        foreach (var (date, taskId, cost) in ParseUsageLog()
            .GroupBy(x => (x.Date, x.TaskId))
            .Select(g => (Date: g.Key.Date, TaskId: g.Key.TaskId, Cost: g.Sum(x => x.Cost)))
            .OrderByDescending(x => x.Date).ThenBy(x => x.TaskId))
        {
            _tableDayByTask.Rows.Add(date.ToDateTime(TimeOnly.MinValue), taskId, cost);
        }

        var planNote = _credentials.IsSubscription ? $"{_credentials.PlanLabel} plan · API-equivalent · " : "";
        _footer.Text = $"{planNote}Updated {DateTime.Now:HH:mm:ss}";
    }

    private IEnumerable<(DateOnly Date, string TaskId, decimal Cost)> ParseUsageLog()
    {
        if (_settings.LogFolder is null) yield break;
        var logFile = Path.Combine(_settings.LogFolder, "usage_log.md");
        if (!File.Exists(logFile)) yield break;

        string? taskId = null;
        DateOnly date = default;

        foreach (var line in File.ReadLines(logFile))
        {
            if (line.StartsWith("## "))
            {
                taskId = null;
                var dashIdx = line.IndexOf(" — ", StringComparison.Ordinal);
                if (dashIdx < 0) continue;
                var candidate = line[3..dashIdx];
                var rest = line[(dashIdx + 3)..];
                if (rest.Length >= 10 && DateOnly.TryParseExact(rest[..10], "yyyy-MM-dd", out var d))
                {
                    taskId = candidate;
                    date = d;
                }
            }
            else if (taskId is not null && line.StartsWith("| **Task total** | **"))
            {
                var inner = line["| **Task total** | **".Length..];
                var end = inner.IndexOf("**", StringComparison.Ordinal);
                if (end > 0 && decimal.TryParse(inner[..end], NumberStyles.Currency, CultureInfo.CurrentCulture, out var cost) && cost > 0)
                    yield return (date, taskId, cost);
                taskId = null;
            }
        }
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        int daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysFromMonday);
    }

    private static DataGridView MakeGrid(DataTable table, params ColDef[] cols)
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            AutoGenerateColumns = false,
            DataSource = table,
        };

        foreach (var def in cols)
        {
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = def.Name,
                Name = def.Name,
                DataPropertyName = def.Name,
            };

            if (def.Fill)
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            else
                col.Width = def.Width;

            if (def.RightAlign)
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            if (def.Format != null)
                col.DefaultCellStyle.Format = def.Format;

            if (def.IsWeekCol)
            {
                var capturedCol = col;
                g.CellFormatting += (_, e) =>
                {
                    if (e.ColumnIndex == capturedCol.Index && e.Value is DateTime weekStart)
                    {
                        var weekEnd = weekStart.AddDays(6);
                        e.Value = weekStart.Year == weekEnd.Year
                            ? $"{weekStart:MMM d} – {weekEnd:MMM d, yyyy}"
                            : $"{weekStart:MMM d, yyyy} – {weekEnd:MMM d, yyyy}";
                        e.FormattingApplied = true;
                    }
                };
            }

            g.Columns.Add(col);
        }

        return g;
    }

    private static TabPage MakeTab(string title, DataGridView grid)
    {
        var tab = new TabPage(title);
        tab.Controls.Add(grid);
        return tab;
    }

    private static Icon LoadIcon()
    {
        var stream = typeof(ReportsForm).Assembly
            .GetManifestResourceStream("ClaudeCostWatch.app_icon.ico");
        return stream is not null ? new Icon(stream) : SystemIcons.Application;
    }
}
