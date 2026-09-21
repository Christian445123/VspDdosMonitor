using System;
using System.Drawing;
using System.Windows.Forms;
using VspDdosMonitor.Services;

namespace VspDdosMonitor.Forms
{
    /// <summary>Fragt nach, ob das gefundene Update installiert werden soll. „Jetzt aktualisieren“ lädt den Installer herunter,
    /// installiert ihn im Hintergrund und startet das Programm danach automatisch neu.</summary>
    public sealed class UpdateForm : Form
    {
        private readonly UpdateInfo _info;
        private readonly AppSettings _settings;
        private readonly ProgressBar _progress = new() { Left = 16, Top = 236, Width = 528, Height = 20, Visible = false };
        private readonly Label _status = new() { Left = 16, Top = 210, Width = 528, Height = 20 };
        private readonly Button _update = new() { Left = 302, Top = 268, Width = 150, Height = 32, Text = "Jetzt aktualisieren" };
        private readonly Button _later = new() { Left = 460, Top = 268, Width = 84, Height = 32, Text = "Später", DialogResult = DialogResult.Cancel };

        public UpdateForm(UpdateInfo info, AppSettings settings)
        {
            _info = info;
            _settings = settings;

            Text = "Update verfügbar";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 316);

            var title = new Label
            {
                Left = 16, Top = 14, Width = 528, Height = 44,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 11f, FontStyle.Bold),
                Text = $"Version {info.Version} ist verfügbar\r\n(installiert: {UpdateService.CurrentVersion})",
            };
            var notes = new TextBox
            {
                Left = 16, Top = 66, Width = 528, Height = 132, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Text = string.IsNullOrWhiteSpace(info.Notes) ? "Keine Versionshinweise vorhanden." : info.Notes.Replace("\n", "\r\n"),
            };

            _update.Click += async (_, _) => await InstallAsync();
            Controls.AddRange(new Control[] { title, notes, _status, _progress, _update, _later });
            AcceptButton = _update;
            CancelButton = _later;
        }

        private async System.Threading.Tasks.Task InstallAsync()
        {
            _update.Enabled = false;
            _later.Enabled = false;
            _progress.Visible = true;
            _status.Text = "Update wird heruntergeladen ...";
            try
            {
                var progress = new Progress<int>(p => { _progress.Value = Math.Min(100, p); _status.Text = $"Update wird heruntergeladen ... {p} %"; });
                var msi = await UpdateService.DownloadAsync(_info, _settings.GitHubToken, progress);
                _status.Text = "Installation wird gestartet – das Programm startet danach automatisch neu ...";
                UpdateService.InstallAndExit(msi);
            }
            catch (Exception ex)
            {
                _status.Text = "Fehler: " + ex.Message;
                _progress.Visible = false;
                _update.Enabled = true;
                _later.Enabled = true;
            }
        }
    }
}
