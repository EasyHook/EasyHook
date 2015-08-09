using System;
using System.Collections.Generic;
using System.Windows.Forms;
using EasyHook;

namespace FileMonitorController
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

            bool noGAC = false;
            try
            {
                Config.Register(
                    "TestFileMonitorController",
                    "FileMonitorController.exe",
                    "FileMonitorInterceptor.dll",
                    "FileMonitorInterface.dll");
            }
            catch (ApplicationException)
            {
                MessageBox.Show("This is an administrative task! Attempting without GAC...", "Permission denied...", MessageBoxButtons.OK);

                noGAC = true;
                //System.Diagnostics.Process.GetCurrentProcess().Kill();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormMain());
        }
    }
}
