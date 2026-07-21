using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace ClaudeCostWatch;

sealed class ModelRatesForm : Form
{
    private const string PricingUrl = "https://claude.com/pricing#api";

    // Only user-set overrides; LiteLLM-sourced rows are display-only unless edited.
    private readonly Dictionary<string, ModelRateOverride> _overrides;
    // All models seen in logs with their current LiteLLM rate (null = unpriced).
    private readonly IReadOnlyDictionary<string, ModelRates?> _seenWithRates;
    private readonly ListView _list;
    private readonly Button _editBtn;
    private readonly Button _removeBtn;

    public IReadOnlyDictionary<string, ModelRateOverride> Result => _overrides;

    public ModelRatesForm(
        IReadOnlyDictionary<string, ModelRateOverride> existingOverrides,
        IReadOnlyDictionary<string, ModelRates?> seenWithRates)
    {
        _overrides = new Dictionary<string, ModelRateOverride>(existingOverrides, StringComparer.OrdinalIgnoreCase);
        _seenWithRates = seenWithRates;

        Text = "Model Rate Overrides";
        Size = new Size(760, 380);
        MinimumSize = new Size(620, 300);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        Icon = LoadIcon();

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _list.Columns.Add("Model", 230);
        _list.Columns.Add("Input/MTok", 85, HorizontalAlignment.Right);
        _list.Columns.Add("Output/MTok", 88, HorizontalAlignment.Right);
        _list.Columns.Add("W5m/MTok", 82, HorizontalAlignment.Right);
        _list.Columns.Add("W1h/MTok", 82, HorizontalAlignment.Right);
        _list.Columns.Add("Read/MTok", 82, HorizontalAlignment.Right);
        _list.DoubleClick += (_, _) => EditSelected();
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();

        var addBtn = new Button { Text = "Add", Width = 75, Location = new Point(8, 8) };
        _editBtn   = new Button { Text = "Edit",   Width = 75, Location = new Point(91, 8) };
        _removeBtn = new Button { Text = "Remove", Width = 75, Location = new Point(174, 8) };

        addBtn.Click    += (_, _) => AddNew();
        _editBtn.Click  += (_, _) => EditSelected();
        _removeBtn.Click += (_, _) => RemoveSelected();

        var legend = new Label
        {
            Text = "Black = your override  ·  Gray = from LiteLLM  ·  Red = no pricing data",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 7.5f),
            Location = new Point(8, 42),
        };

        var link = new LinkLabel { Text = "Anthropic pricing →", AutoSize = true, Location = new Point(8, 58) };
        link.LinkClicked += (_, _) =>
            Process.Start(new ProcessStartInfo(PricingUrl) { UseShellExecute = true });

        var btnPanel = new Panel { Height = 82, Dock = DockStyle.Bottom };
        btnPanel.Controls.AddRange([addBtn, _editBtn, _removeBtn, legend, link]);

        var ok     = new Button { Text = "OK",     DialogResult = DialogResult.OK,     Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };

        var okPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(4),
        };
        okPanel.Controls.AddRange([cancel, ok]);

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.Add(_list);
        Controls.Add(btnPanel);
        Controls.Add(okPanel);

        PopulateList();
        UpdateButtons();
    }

    private void PopulateList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();

        // All seen models, sorted: overrides first, then LiteLLM-priced, then unpriced.
        var allModels = _seenWithRates.Keys
            .Union(_overrides.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(m => _overrides.ContainsKey(m) ? 0 : _seenWithRates.TryGetValue(m, out var r) && r is not null ? 1 : 2)
            .ThenBy(m => m, StringComparer.OrdinalIgnoreCase);

        foreach (var model in allModels)
        {
            if (_overrides.TryGetValue(model, out var ov))
            {
                // User override — black text
                var item = MakeItem(model, ov, SystemColors.WindowText);
                item.Tag = "override";
                _list.Items.Add(item);
            }
            else if (_seenWithRates.TryGetValue(model, out var rates) && rates is not null)
            {
                // LiteLLM-sourced — gray text, shown for reference
                var item = MakeItem(model, FromModelRates(rates), SystemColors.GrayText);
                item.Tag = "litellm";
                _list.Items.Add(item);
            }
            else
            {
                // No pricing data — red text
                var item = new ListViewItem(model) { ForeColor = Color.Firebrick, Tag = "unpriced" };
                item.SubItems.Add("-");
                item.SubItems.Add("-");
                item.SubItems.Add("-");
                item.SubItems.Add("-");
                item.SubItems.Add("-");
                _list.Items.Add(item);
            }
        }

        _list.EndUpdate();
    }

    private static ListViewItem MakeItem(string model, ModelRateOverride r, Color color)
    {
        var item = new ListViewItem(model) { ForeColor = color };
        item.SubItems.Add(r.InputPerMillion.ToString("0.####"));
        item.SubItems.Add(r.OutputPerMillion.ToString("0.####"));
        item.SubItems.Add(r.CacheWrite5mPerMillion.ToString("0.####"));
        item.SubItems.Add(r.CacheWrite1hPerMillion == 0 ? "(=5m)" : r.CacheWrite1hPerMillion.ToString("0.####"));
        item.SubItems.Add(r.CacheReadPerMillion.ToString("0.####"));
        return item;
    }

    private static ModelRateOverride FromModelRates(ModelRates r) => new()
    {
        InputPerMillion        = r.InputPerToken        * 1_000_000m,
        OutputPerMillion       = r.OutputPerToken       * 1_000_000m,
        CacheWrite5mPerMillion = r.CacheWrite5mPerToken * 1_000_000m,
        CacheWrite1hPerMillion = r.CacheWrite1hPerToken * 1_000_000m,
        CacheReadPerMillion    = r.CacheReadPerToken    * 1_000_000m,
    };

    private void UpdateButtons()
    {
        var sel = _list.SelectedItems.Count > 0 ? _list.SelectedItems[0] : null;
        _editBtn.Enabled   = sel is not null;
        _removeBtn.Enabled = sel?.Tag as string == "override";
    }

    private void AddNew()
    {
        using var dlg = new ModelRateEditDialog(null, null);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _overrides[dlg.ModelName] = dlg.Override;
        PopulateList();
    }

    private void EditSelected()
    {
        if (_list.SelectedItems.Count == 0) return;
        var item  = _list.SelectedItems[0];
        var model = item.Text;

        // Pre-fill from the current effective rates (override or LiteLLM).
        ModelRateOverride? current = _overrides.TryGetValue(model, out var ov) ? ov
            : _seenWithRates.TryGetValue(model, out var r) && r is not null ? FromModelRates(r)
            : null;

        using var dlg = new ModelRateEditDialog(model, current);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _overrides[model] = dlg.Override;
        PopulateList();
    }

    private void RemoveSelected()
    {
        if (_list.SelectedItems.Count == 0) return;
        _overrides.Remove(_list.SelectedItems[0].Text);
        PopulateList();
    }

    private static Icon LoadIcon()
    {
        var stream = typeof(ModelRatesForm).Assembly
            .GetManifestResourceStream("ClaudeCostWatch.app_icon.ico");
        return stream is not null ? new Icon(stream) : SystemIcons.Application;
    }
}

sealed class ModelRateEditDialog : Form
{
    private const string PricingUrl = "https://claude.com/pricing#api";

    private readonly TextBox _model;
    private readonly TextBox _input;
    private readonly TextBox _output;
    private readonly TextBox _cw5m;
    private readonly TextBox _cw1h;
    private readonly TextBox _cr;

    public string ModelName => _model.Text.Trim();

    public ModelRateOverride Override => new()
    {
        InputPerMillion        = ParseDecimal(_input.Text),
        OutputPerMillion       = ParseDecimal(_output.Text),
        CacheWrite5mPerMillion = ParseDecimal(_cw5m.Text),
        CacheWrite1hPerMillion = ParseDecimal(_cw1h.Text),
        CacheReadPerMillion    = ParseDecimal(_cr.Text),
    };

    public ModelRateEditDialog(string? model, ModelRateOverride? existing)
    {
        Text = model is null ? "Add Model Rates" : "Edit Model Rates";
        Size = new Size(380, 315);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;

        var table = new TableLayoutPanel
        {
            Location = new Point(12, 12),
            Size = new Size(340, 215),
            ColumnCount = 2,
            RowCount = 6,
            AutoSize = true,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));

        _model  = AddRow(table, 0, "Model name",              model ?? "",   readOnly: model is not null);
        _input  = AddRow(table, 1, "Input ($/MTok)",          Fmt(existing?.InputPerMillion));
        _output = AddRow(table, 2, "Output ($/MTok)",         Fmt(existing?.OutputPerMillion));
        _cw5m   = AddRow(table, 3, "Cache write 5m ($/MTok)", Fmt(existing?.CacheWrite5mPerMillion));
        _cw1h   = AddRow(table, 4, "Cache write 1h ($/MTok)", existing?.CacheWrite1hPerMillion is 0 or null ? "" : Fmt(existing.CacheWrite1hPerMillion));
        _cr     = AddRow(table, 5, "Cache read ($/MTok)",     Fmt(existing?.CacheReadPerMillion));

        var hint = new Label
        {
            Text = "Rates in $ per million tokens. Leave 1h blank to use 5m rate.",
            ForeColor = SystemColors.GrayText,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 7.5f),
            Location = new Point(12, 234),
            AutoSize = true,
        };

        var link = new LinkLabel { Text = "Anthropic pricing →", AutoSize = true, Location = new Point(12, 254) };
        link.LinkClicked += (_, _) =>
            Process.Start(new ProcessStartInfo(PricingUrl) { UseShellExecute = true });

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(196, 250),
            Width = 75,
        };
        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(277, 250),
            Width = 75,
        };

        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_model.Text))
            {
                MessageBox.Show("Model name is required.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange([table, hint, link, ok, cancel]);
    }

    private static TextBox AddRow(TableLayoutPanel table, int row, string label, string value, bool readOnly = false)
    {
        table.Controls.Add(new Label { Text = label, Anchor = AnchorStyles.Left | AnchorStyles.Right, AutoSize = true }, 0, row);
        var tb = new TextBox
        {
            Text = value,
            ReadOnly = readOnly,
            Width = 150,
            BackColor = readOnly ? SystemColors.Control : SystemColors.Window,
        };
        table.Controls.Add(tb, 1, row);
        return tb;
    }

    private static string Fmt(decimal? v) => v is null or 0 ? "" : v.Value.ToString("0.####");

    private static decimal ParseDecimal(string s) =>
        decimal.TryParse(s.Trim(), out var v) ? v : 0m;
}
