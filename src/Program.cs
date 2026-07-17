using System.Threading;
using System.Windows.Forms;

namespace ClaudeCostWatch;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "ClaudeCostWatch", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("ClaudeCostWatch is already running.", "ClaudeCostWatch",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
