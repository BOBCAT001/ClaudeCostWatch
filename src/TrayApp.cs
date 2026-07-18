using Microsoft.Win32;
using System.Windows.Forms;

namespace ClaudeCostWatch;

sealed class TrayApp : ApplicationContext
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ClaudeCostWatch";

    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly LiteLlmPricingProvider _pricing;
    private readonly CostAggregator _aggregator;
    private readonly LogWatcher _watcher;
    private DateTime _lastScanDate = DateTime.Today;
    private BreakdownForm? _breakdownForm;

    public TrayApp()
    {
        _aggregator = new CostAggregator();
        _pricing = new LiteLlmPricingProvider();
        var calculator = new CostCalculator(_pricing);
        _watcher = new LogWatcher(calculator, _aggregator);

        _trayIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "ClaudeCostWatch — loading...",
            ContextMenuStrip = BuildContextMenu(),
            Visible = true
        };

        _timer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        _ = InitAsync();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (DateTime.Today != _lastScanDate)
        {
            _lastScanDate = DateTime.Today;
            await _watcher.RescanAsync();
        }
        UpdateTooltip();
    }

    private async Task InitAsync()
    {
        await _pricing.LoadAsync();
        await _watcher.StartAsync();
        UpdateTooltip();
    }

    private void UpdateTooltip()
    {
        var (daily, monthly) = _aggregator.GetTotals();
        var now = DateTime.Now;
        var text = $"ClaudeCostWatch — {now:MMMM yyyy}\nToday:  {FormatCost(daily)}\nMonth: {FormatCost(monthly)}";
        _trayIcon.Text = text.Length > 127 ? text[..127] : text;
    }

    private static Icon LoadIcon()
    {
        var stream = typeof(TrayApp).Assembly
            .GetManifestResourceStream("ClaudeCostWatch.app_icon.ico");
        return stream is not null ? new Icon(stream) : SystemIcons.Application;
    }

    private static string FormatCost(decimal? cost) =>
        cost.HasValue ? cost.Value.ToString("C2") : "$?.??";

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) is string path
            && path.Equals(Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
    }

    private static void SetStartup(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)!;
        if (enable)
            key.SetValue(AppName, Environment.ProcessPath!);
        else
            key.DeleteValue(AppName, throwOnMissingValue: false);
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var startupItem = new ToolStripMenuItem("Start with Windows");
        startupItem.Click += (_, _) =>
        {
            SetStartup(!IsStartupEnabled());
            startupItem.Checked = IsStartupEnabled();
        };

        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => startupItem.Checked = IsStartupEnabled();

        menu.Items.Add("Project breakdown", null, (_, _) => ShowBreakdown());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open log folder", null, (_, _) => _watcher.OpenLogFolder());
        menu.Items.Add("Refresh now", null, async (_, _) =>
        {
            await _watcher.RescanAsync();
            UpdateTooltip();
        });
        menu.Items.Add("Refresh pricing", null, async (_, _) =>
        {
            await _pricing.RefreshAsync();
            await _watcher.RescanAsync();
            UpdateTooltip();
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            Application.Exit();
        });
        return menu;
    }

    private void ShowBreakdown()
    {
        if (_breakdownForm is null || _breakdownForm.IsDisposed)
            _breakdownForm = new BreakdownForm(_aggregator);

        _breakdownForm.RefreshData();
        _breakdownForm.Show();
        _breakdownForm.BringToFront();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _trayIcon.Dispose();
            _watcher.Dispose();
            _breakdownForm?.Dispose();
        }
        base.Dispose(disposing);
    }
}
