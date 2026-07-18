using System.IO;
using System.Windows.Forms;

namespace ClaudeCostWatch;

sealed class BreakdownForm : Form
{
    private readonly CostAggregator _aggregator;
    private readonly ListView _list;
    private readonly Label _footer;

    public BreakdownForm(CostAggregator aggregator)
    {
        _aggregator = aggregator;

        Text = "Project Breakdown";
        Size = new Size(600, 340);
        MinimumSize = new Size(460, 220);
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        Icon = LoadIcon();

        var screen = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(screen.Right - Width - 8, screen.Bottom - Height - 8);

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            ShowItemToolTips = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _list.Columns.Add("Project", 280);
        _list.Columns.Add("Today", 95, HorizontalAlignment.Right);
        _list.Columns.Add("Week", 95, HorizontalAlignment.Right);
        _list.Columns.Add("Month", 95, HorizontalAlignment.Right);

        _footer = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 6, 0),
            ForeColor = SystemColors.GrayText,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 8f),
        };

        Controls.Add(_list);
        Controls.Add(_footer);

        RefreshData();
    }

    public void RefreshData()
    {
        var projects = _aggregator.GetProjectTotals();

        // Decode all names first so we can detect collisions.
        var decoded = projects.Keys.ToDictionary(k => k, k => DecodeProjectName(k));
        var duplicates = decoded.Values
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _list.BeginUpdate();
        _list.Items.Clear();

        foreach (var (encoded, costs) in projects.OrderByDescending(p => p.Value.Monthly))
        {
            var display = decoded[encoded];
            if (duplicates.Contains(display))
                display = DisambiguateName(encoded, display);

            var item = new ListViewItem(display)
            {
                ToolTipText = encoded,
            };
            item.SubItems.Add(FormatCost(costs.Daily));
            item.SubItems.Add(FormatCost(costs.Weekly));
            item.SubItems.Add(FormatCost(costs.Monthly));
            _list.Items.Add(item);
        }

        if (_list.Items.Count == 0)
        {
            var (daily, _, _) = _aggregator.GetTotals();
            var msg = daily is null ? "Pricing data not yet available." : "No usage found this month.";
            var placeholder = new ListViewItem(msg) { ForeColor = SystemColors.GrayText };
            placeholder.SubItems.Add("");
            placeholder.SubItems.Add("");
            placeholder.SubItems.Add("");
            _list.Items.Add(placeholder);
        }

        _list.EndUpdate();
        _footer.Text = $"Updated {DateTime.Now:HH:mm:ss}";
    }

    // Tries to decode E--gitrepos-ProjectName → ProjectName by reconstructing the Windows path.
    // Falls back to the raw encoded name when the decoded path doesn't exist on disk (e.g. hyphens in dir names).
    private static string DecodeProjectName(string encoded)
    {
        if (encoded.Length >= 3 && encoded[1] == '-' && encoded[2] == '-')
        {
            var rest = encoded[3..].Replace('-', Path.DirectorySeparatorChar);
            var candidate = $@"{char.ToUpper(encoded[0])}:\{rest}";
            if (Directory.Exists(candidate))
                return Path.GetFileName(candidate)!;
        }
        return encoded;
    }

    // When two projects share the same folder name, prepend the parent directory.
    private static string DisambiguateName(string encoded, string shortName)
    {
        if (encoded.Length >= 3 && encoded[1] == '-' && encoded[2] == '-')
        {
            var rest = encoded[3..].Replace('-', Path.DirectorySeparatorChar);
            var candidate = $@"{char.ToUpper(encoded[0])}:\{rest}";
            var parent = Path.GetFileName(Path.GetDirectoryName(candidate) ?? "");
            if (!string.IsNullOrEmpty(parent))
                return $"{parent}\\{shortName}";
        }
        return encoded;
    }

    private static string FormatCost(decimal cost) => cost.ToString("C2");

    private static Icon LoadIcon()
    {
        var stream = typeof(BreakdownForm).Assembly
            .GetManifestResourceStream("ClaudeCostWatch.app_icon.ico");
        return stream is not null ? new Icon(stream) : SystemIcons.Application;
    }
}
