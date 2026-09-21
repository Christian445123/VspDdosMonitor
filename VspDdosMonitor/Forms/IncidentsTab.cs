using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using VspDdosMonitor.Services;

namespace VspDdosMonitor.Forms
{
    /// <summary>Vorfälle mit Details und auffälligen IP-Adressen (Blockierbefehle nur als Vorschlag, nichts wird automatisch ausgeführt).</summary>
    public sealed class IncidentsTab : UserControl
    {
        private readonly ApiClient _api;
        private readonly ListView _incidents = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false };
        private readonly ListView _suspects = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false };
        private readonly TextBox _details = new() { Dock = DockStyle.Top, Height = 74, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private int _selectedIncidentId;

        public IncidentsTab(ApiClient api)
        {
            _api = api;
            Dock = DockStyle.Fill;

            _incidents.Columns.Add("#", 45);
            _incidents.Columns.Add("Server", 120);
            _incidents.Columns.Add("Beginn", 140);
            _incidents.Columns.Add("Ende", 140);
            _incidents.Columns.Add("Status", 75);
            _incidents.Columns.Add("Spitze MBit/s", 95);
            _incidents.Columns.Add("Spitze pps", 90);
            _incidents.Columns.Add("Spitze Verb.", 90);
            _incidents.Columns.Add("Auslöser", 300);
            _incidents.SelectedIndexChanged += async (_, _) => await OnIncidentSelected();

            _suspects.Columns.Add("IP", 140);
            _suspects.Columns.Add("Verbindungen", 95);
            _suspects.Columns.Add("SYN-RECV", 80);
            _suspects.Columns.Add("Vorschlag (nftables)", 300);
            _suspects.Columns.Add("Status", 110);

            var menu = new ContextMenuStrip();
            menu.Items.Add("Befehl kopieren", null, (_, _) => CopyCommand());
            menu.Items.Add("Als blockiert markieren", null, async (_, _) => await SetStatus("manual_blocked"));
            menu.Items.Add("Ignorieren", null, async (_, _) => await SetStatus("ignored"));
            _suspects.ContextMenuStrip = menu;

            var refresh = new Button { Text = "Aktualisieren", Dock = DockStyle.Top, Height = 28 };
            refresh.Click += async (_, _) => await RefreshAsync();

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 230 };
            var top = new GroupBox { Text = "Alle Vorfälle", Dock = DockStyle.Fill };
            top.Controls.Add(_incidents);
            top.Controls.Add(refresh);
            var bottom = new GroupBox { Text = "Details und auffällige IP-Adressen (rechte Maustaste für Aktionen; es wird nichts automatisch blockiert)", Dock = DockStyle.Fill };
            bottom.Controls.Add(_suspects);
            bottom.Controls.Add(_details);
            split.Panel1.Controls.Add(top);
            split.Panel2.Controls.Add(bottom);
            Controls.Add(split);
        }

        public async Task RefreshAsync()
        {
            try
            {
                var result = await _api.GetIncidentsAsync(200);
                _incidents.BeginUpdate();
                _incidents.Items.Clear();
                foreach (var i in result.Incidents)
                {
                    _incidents.Items.Add(new ListViewItem(new[]
                    {
                        i.Id.ToString(), i.ServerName, i.StartedAt, i.ResolvedAt ?? "–", i.Status,
                        i.PeakMbitIn.ToString("N1"), i.PeakPpsIn.ToString("N0"), i.PeakTotalConn.ToString("N0"), i.TriggerReason,
                    }) { Tag = i.Id });
                }
                _incidents.EndUpdate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Vorfälle konnten nicht geladen werden:\n" + ex.Message, "VSRP DDoS Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task OnIncidentSelected()
        {
            if (_incidents.SelectedItems.Count == 0) return;
            _selectedIncidentId = (int)_incidents.SelectedItems[0].Tag!;
            await LoadSuspects();
        }

        private async Task LoadSuspects()
        {
            try
            {
                var r = await _api.GetIncidentAsync(_selectedIncidentId);
                var i = r.Incident;
                _details.Text =
                    $"Vorfall #{i.Id} auf {i.ServerName} ({i.Status})\r\nBeginn: {i.StartedAt}   Ende: {i.ResolvedAt ?? "– (noch aktiv)"}\r\n" +
                    $"Spitze: {i.PeakMbitIn:N1} MBit/s, {i.PeakPpsIn:N0} Pakete/s, {i.PeakTotalConn:N0} Verbindungen (SYN-RECV {i.PeakSynRecv:N0})\r\n" +
                    $"Auslöser: {i.TriggerReason}   Benachrichtigt: E-Mail {(i.NotifiedEmail ? "ja" : "nein")}, Discord {(i.NotifiedDiscord ? "ja" : "nein")}";
                _suspects.BeginUpdate();
                _suspects.Items.Clear();
                foreach (var s in r.Suspects)
                {
                    _suspects.Items.Add(new ListViewItem(new[] { s.Ip, s.ConnCount.ToString("N0"), s.SynRecvCount.ToString("N0"), s.SuggestedCmdNft, s.Status }) { Tag = s.Id });
                }
                _suspects.EndUpdate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Details konnten nicht geladen werden:\n" + ex.Message, "VSRP DDoS Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CopyCommand()
        {
            if (_suspects.SelectedItems.Count == 0) return;
            try { Clipboard.SetText(_suspects.SelectedItems[0].SubItems[3].Text); } catch (Exception) { }
        }

        private async Task SetStatus(string status)
        {
            if (_suspects.SelectedItems.Count == 0) return;
            try
            {
                await _api.SetSuspectStatusAsync((int)_suspects.SelectedItems[0].Tag!, status);
                await LoadSuspects();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Fehler: " + ex.Message, "VSRP DDoS Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
