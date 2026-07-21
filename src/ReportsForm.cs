using System.Data;
using System.Windows.Forms;

namespace ClaudeCostWatch;

sealed class ReportsForm : Form
{
    private readonly CostAggregator _aggregator;
    private readonly ClaudeCredentials _credentials;
    private readonly DataGridView _gridByDay;
    private readonly DataGridView _gridByWeek;
    private readonly DataGridView _gridProjectByDay;
    private readonly DataGridView _gridDayByProject;
    private readonly DataGridView _gridProjectTotals;
    private readonly DataTable _tableByDay = new();
    private readonly DataTable _tableByWeek = new();
    private readonly DataTable _tableProjectByDay = new();
    private readonly DataTable _tableDayByProject = new();
    private readonly DataTable _tableProjectTotals = new();
    private readonly Label _footer;

    private record ColDef(string Name, int Width = 0, string? Format = null, bool RightAlign = false, bool Fill = false, bool IsWeekCol = false);

    public ReportsForm(CostAggregator aggregator, ClaudeCredentials credentials)
    {
        _aggregator = aggregator;
        _credentials = credentials;

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

        Controls.Add(tabs);
        Controls.Add(_footer);
    }

    public void RefreshData()
    {
        var (dailyCosts, dayProjectCosts) = _aggregator.GetHistoricalData();

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

        var planNote = _credentials.IsSubscription ? $"{_credentials.PlanLabel} plan · API-equivalent · " : "";
        _footer.Text = $"{planNote}Updated {DateTime.Now:HH:mm:ss}";
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
