using System;
using System.Windows.Forms;

namespace ECUFileOrganizer
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool startMinimized = Array.Exists(args,
                a => a == "--minimized" || a == "--tray");

            var mainWindow = new MainWindow();

            if (startMinimized)
            {
                mainWindow.WindowState = FormWindowState.Minimized;
                mainWindow.ShowInTaskbar = false;
                mainWindow.Visible = false;
                // Show tray notification after form loads
                mainWindow.Load += (s, e) =>
                {
                    mainWindow.Hide();
                    mainWindow.ShowTrayMessage(
                        "ECU File Organizer",
                        "Running in system tray. Double-click icon to show window.",
                        ToolTipIcon.Info, 2000);
                };
            }
            else
            {
                mainWindow.Show();
            }

            Application.Run(mainWindow);
        }
    }
}
