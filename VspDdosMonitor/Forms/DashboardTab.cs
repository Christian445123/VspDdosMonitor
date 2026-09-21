using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using VspDdosMonitor.Services;

namespace VspDdosMonitor.Forms
{
    /// <summary>
    /// Dashboard je Server (Hauptserver oder überwachter Server, per Auswahl oben): Status, ein-/ausgehende Datenrate und Pakete,
    /// Verbindungen, Verlaufsdiagramme und letzte Vorfälle – wie im Web-Dashboard.
    /// </summary>
    public sealed class DashboardTab : UserControl
    {
        private sealed class ServerChoice
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public override string ToString() => Name;
        }

        private readonly ApiClient _api;
        private readonly ComboBox _serverPicker = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        private readonly Label _banner = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
        private readonly Label _cardMbitIn = Card();
        private readonly Label _cardMbitOut = Card();
        private readonly Label _cardPpsIn = Card();
        private readonly Label _cardPpsOut = Card();
        private readonly Label _cardConn = Card();
        private readonly Label _cardSyn = Card();
        private readonly Chart _rateChart = new() { Dock = DockStyle.Fill };
        private readonly Chart _packetChart = new() { Dock = DockStyle.Fill };
        private readonly Chart _connChart = new() { Dock = DockStyle.Fill };
        private readonly ListView _servers = NewList();
        private readonly ListView _incidents = NewList();
        private bool _loadingPicker;

        private int SelectedServerId => (_serverPicker.SelectedItem as ServerChoice)?.Id ?? 0;

        public DashboardTab(ApiClient api)
        {
            _api = api;
            Dock = DockStyle.Fill;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));

            var picker = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(2, 3, 0, 0) };
            picker.Controls.Add(new Label { Text = "Server:", AutoSize = true, Margin = new Padding(0, 5, 6, 0) });
            picker.Controls.Add(_serverPicker);
            _serverPicker.Items.Add(new ServerChoice { Id = 0, Name = "Hauptserver" });
            _serverPicker.SelectedIndex = 0;
            _serverPicker.SelectedIndexChanged += async (_, _) => { if (!_loadingPicker) await RefreshAsync(); };

            var cards = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(4) };
            AddCard(cards, _cardMbitIn, "Eingehend (MBit/s)");
            AddCard(cards, _cardMbitOut, "Ausgehend (MBit/s)");
            AddCard(cards, _cardPpsIn, "Pakete/s eingehend");
            AddCard(cards, _cardPpsOut, "Pakete/s ausgehend");
            AddCard(cards, _cardConn, "Verbindungen");
            AddCard(cards, _cardSyn, "SYN-RECV");

            ConfigureChart(_rateChart, "MBit/s eingehend", "MBit/s ausgehend", Color.SteelBlue, Color.SeaGreen);
            ConfigureChart(_packetChart, "Pakete/s eingehend", "Pakete/s ausgehend", Color.Firebrick, Color.DarkOrange);
            ConfigureChart(_connChart, "Verbindungen", "SYN-RECV", Color.MediumPurple, Color.Chocolate);

            var charts = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            for (var i = 0; i < 3; i++) charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            charts.Controls.Add(Group("Datenrate ein / aus", _rateChart), 0, 0);
            charts.Controls.Add(Group("Pakete pro Sekunde ein / aus", _packetChart), 1, 0);
            charts.Controls.Add(Group("Verbindungen / SYN-RECV", _connChart), 2, 0);

            _servers.Columns.Add("Server", 150);
            _servers.Columns.Add("Zuletzt gemeldet", 140);
            _servers.Columns.Add("Eingehend", 90);
            _servers.Columns.Add("Status", 110);
            _incidents.Columns.Add("#", 40);
            _incidents.Columns.Add("Server", 110);
            _incidents.Columns.Add("Beginn", 135);
            _incidents.Columns.Add("Ende", 135);
            _incidents.Columns.Add("Status", 70);
            _incidents.Columns.Add("Spitze MBit/s", 90);

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            bottom.Controls.Add(Group("Überwachte Server (Doppelklick öffnet das Dashboard)", _servers), 0, 0);
            bottom.Controls.Add(Group("Letzte Vorfälle dieses Servers", _incidents), 1, 0);
            _servers.DoubleClick += (_, _) => SelectServerFromList();

            layout.Controls.Add(picker, 0, 0);
            layout.Controls.Add(_banner, 0, 1);
            layout.Controls.Add(cards, 0, 2);
            layout.Controls.Add(charts, 0, 3);
            layout.Controls.Add(bottom, 0, 4);
            Controls.Add(layout);
        }

        private static Label Card() => new()
        {
            Text = "–",
            Dock = DockStyle.Fill,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 16f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        private static void AddCard(FlowLayoutPanel host, Label value, string caption)
        {
            var panel = new Panel { Width = 134, Height = 66, Margin = new Padding(0, 0, 5, 0) };
            panel.Controls.Add(value);
            panel.Controls.Add(new Label { Text = caption, Dock = DockStyle.Top, Height = 18, ForeColor = Color.DimGray });
            host.Controls.Add(panel);
        }

        private static ListView NewList() => new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false };

        private static GroupBox Group(string title, Control content)
        {
            var box = new GroupBox { Text = title, Dock = DockStyle.Fill };
            box.Controls.Add(content);
            return box;
        }

        private static void ConfigureChart(Chart chart, string first, string second, Color c1, Color c2)
        {
            var area = new ChartArea("main");
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisX.LabelStyle.Enabled = false;
            chart.ChartAreas.Add(area);
            chart.Legends.Add(new Legend("legend") { Docking = Docking.Top });
            chart.Series.Add(new Series(first) { ChartType = SeriesChartType.Line, ChartArea = "main", Color = c1, BorderWidth = 2 });
            chart.Series.Add(new Series(second) { ChartType = SeriesChartType.Line, ChartArea = "main", Color = c2, BorderWidth = 2 });
        }

        private void SelectServerFromList()
        {
            if (_servers.SelectedItems.Count == 0) return;
            var id = (int)_servers.SelectedItems[0].Tag!;
            foreach (var item in _serverPicker.Items)
            {
                if (item is ServerChoice c && c.Id == id) { _serverPicker.SelectedItem = item; return; }
            }
        }

        private static void Fill(Chart chart, IEnumerable<SampleDto> samples, Func<SampleDto, double> a, Func<SampleDto, double> b)
        {
            chart.Series[0].Points.Clear();
            chart.Series[1].Points.Clear();
            foreach (var s in samples)
            {
                var label = s.Ts.Length >= 19 ? s.Ts.Substring(11, 8) : s.Ts;
                chart.Series[0].Points.AddXY(label, a(s));
                chart.Series[1].Points.AddXY(label, b(s));
            }
        }

        public async Task RefreshAsync()
        {
            try
            {
                var serverId = SelectedServerId;
                var statusTask = _api.GetStatusAsync(serverId);
                var samplesTask = _api.GetSamplesAsync(60, serverId);
                var serversTask = _api.GetServersAsync();
                var incidentsTask = _api.GetIncidentsAsync(8, serverId);
                await Task.WhenAll(statusTask, samplesTask, serversTask, incidentsTask);

                UpdatePicker(serversTask.Result.Servers.Where(x => x.RevokedAt == null).ToList());

                var name = (_serverPicker.SelectedItem as ServerChoice)?.Name ?? "Hauptserver";
                var s = statusTask.Result.LatestSample;
                _cardMbitIn.Text = s is null ? "–" : s.MbitIn.ToString("N1");
                _cardMbitOut.Text = s is null ? "–" : s.MbitOut.ToString("N1");
                _cardPpsIn.Text = s is null ? "–" : s.PpsIn.ToString("N0");
                _cardPpsOut.Text = s is null ? "–" : s.PpsOut.ToString("N0");
                _cardConn.Text = s is null ? "–" : s.TotalConn.ToString("N0");
                _cardSyn.Text = s is null ? "–" : s.SynRecv.ToString("N0");

                if (statusTask.Result.ActiveIncident is { } inc)
                {
                    _banner.Text = $"⚠️ Aktiver Vorfall #{inc.Id} auf {name} seit {inc.StartedAt} — {inc.TriggerReason}";
                    _banner.BackColor = Color.FromArgb(255, 226, 224);
                    _banner.ForeColor = Color.FromArgb(170, 30, 20);
                }
                else
                {
                    _banner.Text = s is null ? $"Noch keine Messwerte von {name}." : $"✅ {name}: kein aktiver Vorfall — Netzwerk unauffällig. (letzte Messung {s.Ts})";
                    _banner.BackColor = s is null ? Color.FromArgb(255, 244, 214) : Color.FromArgb(228, 248, 232);
                    _banner.ForeColor = s is null ? Color.FromArgb(150, 100, 0) : Color.FromArgb(20, 110, 45);
                }

                var samples = samplesTask.Result.Samples;
                Fill(_rateChart, samples, x => x.MbitIn, x => x.MbitOut);
                Fill(_packetChart, samples, x => x.PpsIn, x => x.PpsOut);
                Fill(_connChart, samples, x => x.TotalConn, x => x.SynRecv);

                _servers.BeginUpdate();
                _servers.Items.Clear();
                var main = new ListViewItem(new[] { "Hauptserver", "lokal", "", "" }) { Tag = 0 };
                _servers.Items.Add(main);
                foreach (var sv in serversTask.Result.Servers.Where(x => x.RevokedAt == null))
                {
                    var status = sv.ActiveIncidents > 0 ? "DDoS-Verdacht" : sv.Online ? "online" : sv.LastSeenAt == null ? "wartet auf Agent" : "keine Meldung";
                    _servers.Items.Add(new ListViewItem(new[] { sv.Name, sv.LastSeenAt ?? "noch nie", sv.LastMbit is { } m ? m.ToString("N1") : "–", status })
                    {
                        Tag = sv.Id,
                        ForeColor = sv.ActiveIncidents > 0 ? Color.Firebrick : sv.Online ? Color.SeaGreen : Color.DarkGoldenrod,
                    });
                }
                _servers.EndUpdate();

                _incidents.BeginUpdate();
                _incidents.Items.Clear();
                foreach (var i in incidentsTask.Result.Incidents)
                {
                    _incidents.Items.Add(new ListViewItem(new[] { i.Id.ToString(), i.ServerName, i.StartedAt, i.ResolvedAt ?? "–", i.Status, i.PeakMbitIn.ToString("N1") }));
                }
                _incidents.EndUpdate();
            }
            catch (Exception ex)
            {
                _banner.Text = "Verbindungsfehler: " + ex.Message;
                _banner.BackColor = Color.FromArgb(255, 244, 214);
                _banner.ForeColor = Color.FromArgb(150, 100, 0);
            }
        }

        /// <summary>Hält die Auswahlliste aktuell (neue Server erscheinen, widerrufene verschwinden), ohne die Auswahl zu verlieren.</summary>
        private void UpdatePicker(List<ServerDto> servers)
        {
            var wanted = new List<ServerChoice> { new ServerChoice { Id = 0, Name = "Hauptserver" } };
            wanted.AddRange(servers.Select(x => new ServerChoice { Id = x.Id, Name = x.Name }));
            var current = SelectedServerId;
            var same = wanted.Count == _serverPicker.Items.Count && wanted.Select((w, i) => ((ServerChoice)_serverPicker.Items[i]).Id == w.Id && ((ServerChoice)_serverPicker.Items[i]).Name == w.Name).All(x => x);
            if (same) return;

            _loadingPicker = true;
            _serverPicker.Items.Clear();
            foreach (var w in wanted) _serverPicker.Items.Add(w);
            var idx = wanted.FindIndex(w => w.Id == current);
            _serverPicker.SelectedIndex = idx >= 0 ? idx : 0;
            _loadingPicker = false;
        }
    }
}
