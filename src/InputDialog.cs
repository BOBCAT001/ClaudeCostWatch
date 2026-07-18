using System.Windows.Forms;

namespace ClaudeCostWatch;

sealed class InputDialog : Form
{
    private readonly TextBox _input;
    public string Value => _input.Text.Trim();

    public InputDialog(string title, string prompt)
    {
        Text = title;
        Size = new Size(360, 145);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;

        var label = new Label { Text = prompt, Location = new Point(12, 14), AutoSize = true };

        _input = new TextBox { Location = new Point(12, 36), Width = 318 };

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(174, 70),
            Width = 75,
        };
        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(255, 70),
            Width = 75,
        };

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange([label, _input, ok, cancel]);
    }
}
