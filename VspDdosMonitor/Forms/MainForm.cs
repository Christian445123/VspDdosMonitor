using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using VspDdosMonitor.Services;

namespace VspDdosMonitor.Forms
{
    /// <summary>Hauptfenster: dieselben Bereiche wie das Web-Dashboard (ohne Lizenz- und API-Verwaltung) als Reiter am rechten Rand.</summary>
    public sealed class MainForm : Form
    {
        private readonly AppSettings _settings;
        private readonly DarkTabControl _tabs = new()
        {
            Dock = DockStyle.Fill,
        };
        private readonly DashboardTab _dashboard;
        private readonly IncidentsTab _incidents;
        private readonly ServersTab _servers;
        private readonly SettingsTab _settingsTab;
        private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 10000 };
        private readonly System.Windows.Forms.Timer _updateTimer = new() { Interval = 30 * 60 * 1000 };
        private bool _updateDialogOpen;
        private Version? _declinedVersion;
        private UpdateInfo? _pendingUpdate;
        private readonly ToolStripMenuItem _updateMenu = new("Update verfügbar") { Visible = false, Alignment = ToolStripItemAlignment.Right };

        public MainForm(AppSettings settings, ApiClient api)
        {
            _settings = settings;
            _dashboard = new DashboardTab(api);
            _incidents = new IncidentsTab(api);
            _servers = new ServersTab(api);
            _settingsTab = new SettingsTab(api);

            Text = "VSRP DDoS Monitor  v" + UpdateService.CurrentVersion;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1080, 720);
            MinimumSize = new Size(900, 600);

            var menu = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("Datei");
            fileMenu.DropDownItems.Add("Verbindung (Lizenz- und API-Schlüssel)...", null, (_, _) => OpenConnectionSettings());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Beenden", null, (_, _) => Close());
            var helpMenu = new ToolStripMenuItem("Hilfe");
            helpMenu.DropDownItems.Add("Nach Updates suchen...", null, async (_, _) => await CheckForUpdatesAsync(manual: true));
            menu.Items.Add(fileMenu);
            menu.Items.Add(helpMenu);
            menu.Items.Add(_updateMenu);
            _updateMenu.Click += async (_, _) => await ShowUpdateDialogAsync(_pendingUpdate);
            MainMenuStrip = menu;

            AddPage("Dashboard", _dashboard);
            AddPage("Vorfälle", _incidents);
            AddPage("Server", _servers);
            AddPage("Einstellungen", _settingsTab);
            _tabs.SelectedIndexChanged += async (_, _) => await RefreshSelectedAsync();

            Controls.Add(_tabs);
            Controls.Add(menu);
            Theme.Apply(this);
            _updateMenu.ForeColor = Theme.Warn;

            _refreshTimer.Tick += async (_, _) => { if (_tabs.SelectedIndex == 0) await _dashboard.RefreshAsync(); };
            _updateTimer.Tick += async (_, _) => { if (_settings.AutoCheckUpdates) await CheckForUpdatesAsync(manual: false); };
            Load += async (_, _) =>
            {
                await RefreshSelectedAsync();
                _refreshTimer.Start();
                _updateTimer.Start();
                if (_settings.AutoCheckUpdates) await CheckForUpdatesAsync(manual: false);
            };
        }

        private void AddPage(string title, Control content)
        {
            var page = new TabPage(title) { Padding = new Padding(6) };
            page.Controls.Add(content);
            _tabs.TabPages.Add(page);
        }

        private async Task RefreshSelectedAsync()
        {
            switch (_tabs.SelectedIndex)
            {
                case 0: await _dashboard.RefreshAsync(); break;
                case 1: await _incidents.RefreshAsync(); break;
                case 2: await _servers.RefreshAsync(); break;
                case 3: await _settingsTab.RefreshAsync(); break;
            }
        }

        private void OpenConnectionSettings()
        {
            using var form = new SettingsForm(_settings, firstRun: false);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                Program.RestartApp();
            }
        }

        /// <summary>Sucht nach einer neuen Version und fragt nach. „Jetzt aktualisieren“ installiert sie automatisch.
        /// Ein wartendes Update bleibt rechts in der Menüleiste sichtbar, auch nach „Später“.</summary>
        private async Task CheckForUpdatesAsync(bool manual)
        {
            if (_updateDialogOpen) return;
            try
            {
                var info = await UpdateService.CheckAsync(_settings.GitHubRepo, _settings.GitHubToken);
                if (info is null)
                {
                    _pendingUpdate = null;
                    _updateMenu.Visible = false;
                    if (manual)
                    {
                        MessageBox.Show(this, "Es ist bereits die aktuelle Version installiert.", "Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                _pendingUpdate = info;
                _updateMenu.Text = $"⬆ Update {info.Version} verfügbar";
                _updateMenu.Visible = true;
                if (!manual && _declinedVersion == info.Version) return;

                await ShowUpdateDialogAsync(info);
            }
            catch (Exception ex)
            {
                if (manual)
                {
                    MessageBox.Show(this, "Update-Prüfung fehlgeschlagen:\n" + ex.Message, "Updates", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private Task ShowUpdateDialogAsync(UpdateInfo? info)
        {
            if (info is null || _updateDialogOpen) return Task.CompletedTask;
            _updateDialogOpen = true;
            try
            {
                using var dialog = new UpdateForm(info, _settings);
                if (dialog.ShowDialog(this) != DialogResult.OK) _declinedVersion = info.Version;
            }
            finally
            {
                _updateDialogOpen = false;
            }
            return Task.CompletedTask;
        }
    }
}
