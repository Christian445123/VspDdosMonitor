using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using VspDdosMonitor.Services;

namespace VspDdosMonitor.Forms
{
    public sealed class MainForm : Form
    {
        private readonly AppSettings _settings;
        private readonly ApiClient _api;
        private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 10000 };

        private readonly Label _statusBanner = new() { Dock = DockStyle.Top, Height = 36, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
        private readonly FlowLayoutPanel _cards = new() { Dock = DockStyle.Top, Height = 90, Padding = new Padding(10) };
        private readonly ListView _incidentsList = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false };
        private readonly ListView _suspectsList = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false };
        private readonly Label _cardMbit = CreateCard();
        private readonly Label _cardPps = CreateCard();
        private readonly Label _cardConn = CreateCard();
        private readonly Label _cardSyn = CreateCard();

        private int _selectedIncidentId;

        public MainForm(AppSettings settings, ApiClient api)
        {
            _settings = settings;
            _api = api;

            Text = "VSRP DDoS Monitor";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(920, 640);

            var menu = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("Datei");
            var settingsItem = new ToolStripMenuItem("Einstellungen...");
            settingsItem.Click += (_, _) => OpenSettings();
            var exitItem = new ToolStripMenuItem("Beenden");
            exitItem.Click += (_, _) => Close();
            fileMenu.DropDownItems.Add(settingsItem);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(exitItem);

            var helpMenu = new ToolStripMenuItem("Hilfe");
            var updateItem = new ToolStripMenuItem("Nach Updates suchen...");
            updateItem.Click += async (_, _) => await CheckForUpdatesAsync(manual: true);
            helpMenu.DropDownItems.Add(updateItem);

            menu.Items.Add(fileMenu);
            menu.Items.Add(helpMenu);
            MainMenuStrip = menu;

            BuildCards();

            _incidentsList.Columns.Add("#", 45);
            _incidentsList.Columns.Add("Server", 110);
            _incidentsList.Columns.Add("Beginn", 140);
            _incidentsList.Columns.Add("Ende", 140);
            _incidentsList.Columns.Add("Status", 80);
            _incidentsList.Columns.Add("Spitze MBit/s", 90);
            _incidentsList.Columns.Add("Auslöser", 260);
            _incidentsList.SelectedIndexChanged += IncidentsList_SelectedIndexChanged;

            _suspectsList.Columns.Add("IP", 130);
            _suspectsList.Columns.Add("Verbindungen", 90);
            _suspectsList.Columns.Add("SYN-RECV", 80);
            _suspectsList.Columns.Add("Befehl (nftables)", 260);
            _suspectsList.Columns.Add("Status", 100);

            var suspectsMenu = new ContextMenuStrip();
            var blockItem = new ToolStripMenuItem("Als blockiert markieren");
            blockItem.Click += (_, _) => UpdateSelectedSuspectStatusAsync("manual_blocked");
            var ignoreItem = new ToolStripMenuItem("Ignorieren");
            ignoreItem.Click += (_, _) => UpdateSelectedSuspectStatusAsync("ignored");
            var copyItem = new ToolStripMenuItem("Befehl kopieren");
            copyItem.Click += (_, _) => CopySelectedSuspectCommand();
            suspectsMenu.Items.Add(copyItem);
            suspectsMenu.Items.Add(blockItem);
            suspectsMenu.Items.Add(ignoreItem);
            _suspectsList.ContextMenuStrip = suspectsMenu;

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 260 };
            var incidentsGroup = new GroupBox { Text = "Vorfälle", Dock = DockStyle.Fill };
            incidentsGroup.Controls.Add(_incidentsList);
            var suspectsGroup = new GroupBox { Text = "Auffällige IP-Adressen (rechte Maustaste für Aktionen)", Dock = DockStyle.Fill };
            suspectsGroup.Controls.Add(_suspectsList);
            split.Panel1.Controls.Add(incidentsGroup);
            split.Panel2.Controls.Add(suspectsGroup);

            Controls.Add(split);
            Controls.Add(_cards);
            Controls.Add(_statusBanner);
            Controls.Add(menu);

            _statusTimer.Tick += async (_, _) => await RefreshStatusAsync();
            Load += async (_, _) =>
            {
                await RefreshStatusAsync();
                await RefreshIncidentsAsync();
                _statusTimer.Start();
                if (_settings.AutoCheckUpdates) await CheckForUpdatesAsync(manual: false);
            };
        }

        private void BuildCards()
        {
            void Add(Label card, string caption)
            {
                var panel = new Panel { Width = 200, Height = 70, Margin = new Padding(0, 0, 12, 0) };
                var captionLabel = new Label { Text = caption, Dock = DockStyle.Top, Height = 18, ForeColor = Color.DimGray, Font = new Font(SystemFonts.DefaultFont.FontFamily, 8f) };
                card.Dock = DockStyle.Fill;
                panel.Controls.Add(card);
                panel.Controls.Add(captionLabel);
                _cards.Controls.Add(panel);
            }
            Add(_cardMbit, "Eingehend (MBit/s)");
            Add(_cardPps, "Pakete/s");
            Add(_cardConn, "Verbindungen");
            Add(_cardSyn, "SYN-RECV");
        }

        private static Label CreateCard() => new()
        {
            Text = "–",
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 20f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        private async void IncidentsList_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_incidentsList.SelectedItems.Count == 0) return;
            var id = (int)_incidentsList.SelectedItems[0].Tag!;
            _selectedIncidentId = id;
            await RefreshSuspectsAsync(id);
        }

        private void CopySelectedSuspectCommand()
        {
            if (_suspectsList.SelectedItems.Count == 0) return;
            var cmd = _suspectsList.SelectedItems[0].SubItems[3].Text;
            try { Clipboard.SetText(cmd); } catch (Exception) { /* ignorieren */ }
        }

        private async void UpdateSelectedSuspectStatusAsync(string status)
        {
            if (_suspectsList.SelectedItems.Count == 0) return;
            var suspectId = (int)_suspectsList.SelectedItems[0].Tag!;
            try
            {
                await _api.SetSuspectStatusAsync(suspectId, status);
                await RefreshSuspectsAsync(_selectedIncidentId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Fehler: " + ex.Message, "VSRP DDoS Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task RefreshStatusAsync()
        {
            try
            {
                var status = await _api.GetStatusAsync();
                var sample = status.LatestSample;
                _cardMbit.Text = sample is null ? "–" : sample.MbitIn.ToString("N1");
                _cardPps.Text = sample is null ? "–" : sample.PpsIn.ToString("N0");
                _cardConn.Text = sample is null ? "–" : sample.TotalConn.ToString("N0");
                _cardSyn.Text = sample is null ? "–" : sample.SynRecv.ToString("N0");

                if (status.ActiveIncident is { } inc)
                {
                    _statusBanner.Text = $"⚠️ Aktiver Vorfall #{inc.Id} auf {inc.ServerName} seit {inc.StartedAt} — {inc.TriggerReason}";
                    _statusBanner.BackColor = Color.FromArgb(255, 235, 235);
                    _statusBanner.ForeColor = Color.FromArgb(180, 30, 20);
                }
                else
                {
                    _statusBanner.Text = "✅ Kein aktiver Vorfall — Netzwerk unauffällig.";
                    _statusBanner.BackColor = Color.FromArgb(232, 250, 236);
                    _statusBanner.ForeColor = Color.FromArgb(20, 120, 50);
                }
            }
            catch (Exception ex)
            {
                _statusBanner.Text = "Verbindungsfehler: " + ex.Message;
                _statusBanner.BackColor = Color.FromArgb(255, 244, 214);
                _statusBanner.ForeColor = Color.FromArgb(150, 100, 0);
            }
        }

        private async Task RefreshIncidentsAsync()
        {
            try
            {
                var result = await _api.GetIncidentsAsync(100);
                _incidentsList.BeginUpdate();
                _incidentsList.Items.Clear();
                foreach (var inc in result.Incidents)
                {
                    var item = new ListViewItem(new[]
                    {
                        inc.Id.ToString(),
                        inc.ServerName,
                        inc.StartedAt,
                        inc.ResolvedAt ?? "–",
                        inc.Status,
                        inc.PeakMbitIn.ToString("N1"),
                        inc.TriggerReason,
                    })
                    { Tag = inc.Id };
                    _incidentsList.Items.Add(item);
                }
                _incidentsList.EndUpdate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Vorfälle konnten nicht geladen werden:\n" + ex.Message, "VSRP DDoS Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task RefreshSuspectsAsync(int incidentId)
        {
            try
            {
                var result = await _api.GetIncidentAsync(incidentId);
                _suspectsList.BeginUpdate();
                _suspectsList.Items.Clear();
                foreach (var s in result.Suspects)
                {
                    var item = new ListViewItem(new[]
                    {
                        s.Ip,
                        s.ConnCount.ToString("N0"),
                        s.SynRecvCount.ToString("N0"),
                        s.SuggestedCmdNft,
                        s.Status,
                    })
                    { Tag = s.Id };
                    _suspectsList.Items.Add(item);
                }
                _suspectsList.EndUpdate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Details konnten nicht geladen werden:\n" + ex.Message, "VSRP DDoS Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenSettings()
        {
            using var form = new SettingsForm(_settings, firstRun: false);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                Program.RestartApp();
            }
        }

        private async Task CheckForUpdatesAsync(bool manual)
        {
            try
            {
                var info = await UpdateService.CheckAsync(_settings.GitHubRepo, _settings.GitHubToken);
                if (info is null)
                {
                    if (manual)
                    {
                        MessageBox.Show(this, "Es ist bereits die aktuelle Version installiert.", "Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                var result = MessageBox.Show(this,
                    $"Version {info.Version} ist verfügbar (aktuell installiert: {UpdateService.CurrentVersion}).\n\n{info.Notes}\n\nJetzt herunterladen und installieren?",
                    "Update verfügbar", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (result != DialogResult.Yes) return;

                var msiPath = await UpdateService.DownloadAsync(info, _settings.GitHubToken);
                UpdateService.InstallAndExit(msiPath);
            }
            catch (Exception ex)
            {
                if (manual)
                {
                    MessageBox.Show(this, "Update-Prüfung fehlgeschlagen:\n" + ex.Message, "Updates", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
