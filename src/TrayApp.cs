using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
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
    private readonly AppSettings _settings;
    private readonly ClaudeCredentials _credentials;
    private readonly UsageLogger _logger = new();
    private DateTime _lastScanDate = DateTime.Today;
    private BreakdownForm? _breakdownForm;
    private ReportsForm? _reportsForm;
    private ToolStripMenuItem? _logToggleItem;
    private ToolStripMenuItem? _openLogItem;

    public TrayApp()
    {
        _settings = AppSettings.Load();
        _credentials = ClaudeCredentials.Load();
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

        MessageBox.Show("ClaudeCodeWatch active and now visible in tray",
            "ClaudeCostWatch", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (DateTime.Today != _lastScanDate)
        {
            _lastScanDate = DateTime.Today;
            await _watcher.RescanAsync();
        }
        UpdateTooltip();
        if (_logger.IsLogging)
            _logger.Update(_aggregator.GetProjectTotals());
    }

    private async Task InitAsync()
    {
        await _pricing.LoadAsync();
        _pricing.SetOverrides(_settings.ModelRateOverrides);
        await _watcher.StartAsync();
        UpdateTooltip();
    }

    private void UpdateTooltip()
    {
        var (daily, weekly, monthly) = _aggregator.GetTotals();
        var now = DateTime.Now;
        var plan = _credentials.IsSubscription ? $" ({_credentials.PlanLabel})" : "";
        var task = _logger.IsLogging ? $" [{_logger.ActiveTaskId}]" : "";
        var equiv = _credentials.IsSubscription ? "~" : "";
        var text = $"ClaudeCostWatch{plan}{task} — {now:MMMM yyyy}\nToday:  {equiv}{FormatCost(daily)}\nWeek:   {equiv}{FormatCost(weekly)}\nMonth: {equiv}{FormatCost(monthly)}";
        if (_logger.IsLogging)
        {
            var taskCost = _logger.GetTaskTotal(_aggregator.GetProjectTotals());
            text += $"\nTask:   {equiv}{taskCost:C2}";
        }
        var unknown = _aggregator.GetUnknownModels();
        if (unknown.Count > 0)
            text += $"\n⚠ {unknown.Count} model(s) unpriced";
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

        _logToggleItem = new ToolStripMenuItem("Start logging...", null, (_, _) =>
        {
            if (_logger.IsLogging)
                StopLogging();
            else
                StartLogging();
        });

        _openLogItem = new ToolStripMenuItem("Open log file", null, (_, _) => OpenLogFile());

        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) =>
        {
            startupItem.Checked = IsStartupEnabled();
            _logToggleItem.Text = _logger.IsLogging
                ? $"Stop logging [{_logger.ActiveTaskId}]"
                : "Start logging...";
            _openLogItem.Enabled = _settings.LogFolder is not null
                && File.Exists(Path.Combine(_settings.LogFolder, "usage_log.md"));
        };

        menu.Items.Add("Project breakdown", null, (_, _) => ShowBreakdown());
        menu.Items.Add("Reports...", null, (_, _) => ShowReports());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_logToggleItem);
        menu.Items.Add(_openLogItem);
        menu.Items.Add("Set log folder...", null, (_, _) => ChooseLogFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open Claude JSONL folder", null, (_, _) => _watcher.OpenLogFolder());
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
        menu.Items.Add("Edit model rates...", null, async (_, _) =>
        {
            var seenWithRates = _aggregator.GetSeenModels()
                .ToDictionary(m => m, m => _pricing.GetRates(m), StringComparer.OrdinalIgnoreCase);
            using var form = new ModelRatesForm(_settings.ModelRateOverrides, seenWithRates);
            if (form.ShowDialog() != DialogResult.OK) return;
            _settings.ModelRateOverrides = new(form.Result, StringComparer.OrdinalIgnoreCase);
            _settings.Save();
            _pricing.SetOverrides(_settings.ModelRateOverrides);
            await _watcher.RescanAsync();
            UpdateTooltip();
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Visit Project Homepage", null, (_, _) =>
            Process.Start(new ProcessStartInfo("https://www.jlion.com/Tools/ClaudeCostWatch") { UseShellExecute = true }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Close Application", null, (_, _) =>
        {
            if (_logger.IsLogging)
                _logger.Stop(_aggregator.GetProjectTotals());
            _trayIcon.Visible = false;
            Application.Exit();
        });
        menu.Items.Add("Close", null, (_, _) => menu.Close());
        return menu;
    }

    private void StartLogging()
    {
        if (_settings.LogFolder is null && !ChooseLogFolder()) return;

        using var dlg = new InputDialog("Start Logging", "Task ID (e.g. PROJ-123 or #456):");
        if (dlg.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.Value)) return;

        _logger.Start(dlg.Value, _settings.LogFolder!, _aggregator.GetProjectTotals());
        UpdateTooltip();
    }

    private void StopLogging()
    {
        _logger.Stop(_aggregator.GetProjectTotals());
        UpdateTooltip();
    }

    private bool ChooseLogFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Choose folder for usage logs",
            UseDescriptionForTitle = true,
            SelectedPath = _settings.LogFolder
                ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dlg.ShowDialog() != DialogResult.OK) return false;
        _settings.LogFolder = dlg.SelectedPath;
        _settings.Save();
        return true;
    }

    private void OpenLogFile()
    {
        if (_settings.LogFolder is null) return;
        var path = Path.Combine(_settings.LogFolder, "usage_log.md");
        if (!File.Exists(path)) return;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void ShowBreakdown()
    {
        if (_breakdownForm is null || _breakdownForm.IsDisposed)
            _breakdownForm = new BreakdownForm(_aggregator, _credentials);

        _breakdownForm.RefreshData();
        _breakdownForm.Show();
        _breakdownForm.BringToFront();
    }

    private void ShowReports()
    {
        if (_reportsForm is null || _reportsForm.IsDisposed)
            _reportsForm = new ReportsForm(_aggregator, _credentials, _settings);

        _reportsForm.RefreshData();
        _reportsForm.Show();
        _reportsForm.BringToFront();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _trayIcon.Dispose();
            _watcher.Dispose();
            _breakdownForm?.Dispose();
            _reportsForm?.Dispose();
        }
        base.Dispose(disposing);
    }
}
