using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using VspDdosMonitor.Forms;
using VspDdosMonitor.Services;

namespace VspDdosMonitor
{
    internal static class Program
    {
        private static Mutex? _instanceMutex;

        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);

        /// <summary>Nur eine Instanz pro Windows-Sitzung: Ein zweiter Start holt das vorhandene Fenster nach vorn und beendet sich.</summary>
        private static bool AcquireSingleInstance(bool restarted)
        {
            _instanceMutex = new Mutex(false, @"Local\VSRP_DDoS_Monitor");
            try
            {
                if (_instanceMutex.WaitOne(restarted ? 10000 : 0)) return true;
            }
            catch (AbandonedMutexException)
            {
                return true;
            }

            try
            {
                var self = Process.GetCurrentProcess();
                foreach (var p in Process.GetProcessesByName(self.ProcessName))
                {
                    if (p.Id == self.Id || p.MainWindowHandle == IntPtr.Zero) continue;
                    if (IsIconic(p.MainWindowHandle)) ShowWindow(p.MainWindowHandle, 9); // SW_RESTORE
                    SetForegroundWindow(p.MainWindowHandle);
                    break;
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        /// <summary>Startet die Anwendung neu (z. B. nach geänderten Einstellungen oder einem Update) und beendet diese Instanz.</summary>
        public static void RestartApp()
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (exe is not null)
            {
                Process.Start(new ProcessStartInfo(exe, "--restart") { UseShellExecute = false });
            }
            Environment.Exit(0);
        }

        [STAThread]
        private static void Main(string[] args)
        {
            if (!AcquireSingleInstance(args.Contains("--restart"))) return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var settings = AppSettings.Load();
            SetupImport.Apply(settings); // Zugangsdaten aus dem Installer übernehmen (nur wenn neu eingegeben)

            while (true)
            {
                if (!settings.IsConfigured)
                {
                    using var setup = new SettingsForm(settings, firstRun: true);
                    if (setup.ShowDialog() != DialogResult.OK) return;
                }

                try
                {
                    var api = new ApiClient(settings);
                    var ping = System.Threading.Tasks.Task.Run(() => api.PingAsync()).GetAwaiter().GetResult();
                    if (!ping.Ok)
                    {
                        throw new ApiException("Unerwartete Antwort vom Server.");
                    }
                    Application.Run(new MainForm(settings, api));
                    return;
                }
                catch (Exception ex)
                {
                    var retry = MessageBox.Show(
                        "Verbindung zur API fehlgeschlagen:\n\n" + ex.Message + "\n\nEinstellungen jetzt anpassen?",
                        "Verbindungsfehler", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                    if (retry != DialogResult.Yes) return;

                    using var form = new SettingsForm(settings, firstRun: false);
                    if (form.ShowDialog() != DialogResult.OK) return;
                }
            }
        }
    }
}
