using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace ClaudeCostWatch;

sealed class ModelRatesForm : Form
{
    private const string PricingUrl = "https://claude.com/pricing#api";

    private readonly Dictionary<string, ModelRateOverride> _overrides;
    private readonly ListView _list;

    public IReadOnlyDictionary<string, ModelRateOverride> Result => _overrides;

    public ModelRatesForm(IReadOnlyDictionary<string, ModelRateOverride> existing)
    {
        _overrides = new Dictionary<string, ModelRateOverride>(existing, StringComparer.OrdinalIgnoreCase);

        Text = "Model Rate Overrides";
        Size = new Size(740, 360);
        MinimumSize = new Size(600, 280);
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
        _list.Columns.Add("Model", 220);
        _list.Columns.Add("Input/MTok", 85, HorizontalAlignment.Right);
        _list.Columns.Add("Output/MTok", 85, HorizontalAlignment.Right);
        _list.Columns.Add("W5m/MTok", 85, HorizontalAlignment.Right);
        _list.Columns.Add("W1h/MTok", 85, HorizontalAlignment.Right);
        _list.Columns.Add("Read/MTok", 85, HorizontalAlignment.Right);
        _list.DoubleClick += (_, _) => EditSelected();

        var addBtn    = new Button { Text = "Add",    Width = 75, Location = new Point(8, 8) };
        var editBtn   = new Button { Text = "Edit",   Width = 75, Location = new Point(91, 8) };
        var removeBtn = new Button { Text = "Remove", Width = 75, Location = new Point(174, 8) };

        addBtn.Click    += (_, _) => AddNew();
        editBtn.Click   += (_, _) => EditSelected();
        removeBtn.Click += (_, _) => RemoveSelected();

        var link = new LinkLabel
        {
            Text = "Anthropic pricing →",
            AutoSize = true,
            Location = new Point(8, 40),
        };
        link.LinkClicked += (_, _) =>
            Process.Start(new ProcessStartInfo(PricingUrl) { UseShellExecute = true });

        var btnPanel = new Panel { Height = 68, Dock = DockStyle.Bottom };
        btnPanel.Controls.AddRange([addBtn, editBtn, removeBtn, link]);

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
    }

    private void PopulateList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var (model, r) in _overrides)
        {
            var item = new ListViewItem(model);
            item.SubItems.Add(r.InputPerMillion.ToString("0.####"));
            item.SubItems.Add(r.OutputPerMillion.ToString("0.####"));
            item.SubItems.Add(r.CacheWrite5mPerMillion.ToString("0.####"));
            item.SubItems.Add(r.CacheWrite1hPerMillion == 0 ? "(=5m)" : r.CacheWrite1hPerMillion.ToString("0.####"));
            item.SubItems.Add(r.CacheReadPerMillion.ToString("0.####"));
            _list.Items.Add(item);
        }
        _list.EndUpdate();
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
        var model = _list.SelectedItems[0].Text;
        using var dlg = new ModelRateEditDialog(model, _overrides[model]);
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
        Size = new Size(380, 310);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;

        var table = new TableLayoutPanel
        {
            Location = new Point(12, 12),
            Size = new Size(340, 210),
            ColumnCount = 2,
            RowCount = 7,
            AutoSize = true,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));

        _model  = AddRow(table, 0, "Model name",             model ?? "", readOnly: model is not null);
        _input  = AddRow(table, 1, "Input ($/MTok)",         existing?.InputPerMillion.ToString("0.####") ?? "");
        _output = AddRow(table, 2, "Output ($/MTok)",        existing?.OutputPerMillion.ToString("0.####") ?? "");
        _cw5m   = AddRow(table, 3, "Cache write 5m ($/MTok)", existing?.CacheWrite5mPerMillion.ToString("0.####") ?? "");
        _cw1h   = AddRow(table, 4, "Cache write 1h ($/MTok)", existing?.CacheWrite1hPerMillion is 0 or null ? "" : existing.CacheWrite1hPerMillion.ToString("0.####"));
        _cr     = AddRow(table, 5, "Cache read ($/MTok)",    existing?.CacheReadPerMillion.ToString("0.####") ?? "");

        var hint = new Label
        {
            Text = "Rates in $ per million tokens. Leave 1h blank to use 5m rate.",
            ForeColor = SystemColors.GrayText,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 7.5f),
            Location = new Point(12, 228),
            AutoSize = true,
        };

        var link = new LinkLabel
        {
            Text = "Anthropic pricing →",
            AutoSize = true,
            Location = new Point(12, 248),
        };
        link.LinkClicked += (_, _) =>
            Process.Start(new ProcessStartInfo(PricingUrl) { UseShellExecute = true });

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(196, 242),
            Width = 75,
        };
        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(277, 242),
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
        var tb = new TextBox { Text = value, ReadOnly = readOnly, Width = 150, BackColor = readOnly ? SystemColors.Control : SystemColors.Window };
        table.Controls.Add(tb, 1, row);
        return tb;
    }

    private static decimal ParseDecimal(string s) =>
        decimal.TryParse(s.Trim(), out var v) ? v : 0m;
}
