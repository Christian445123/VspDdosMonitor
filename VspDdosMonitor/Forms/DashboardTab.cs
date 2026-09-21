using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using VspDdosMonitor.Services;

namespace VspDdosMonitor.Forms
{
    /// <summary>Dashboard: Status, aktuelle Werte des Hauptservers, Verlauf, überwachte Server und letzte Vorfälle.</summary>
    public sealed class DashboardTab : UserControl
    {
        private readonly ApiClient _api;
        private readonly Label _banner = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
        private readonly Label _cardMbit = Card();
        private readonly Label _cardPps = Card();
        private readonly Label _cardConn = Card();
        private readonly Label _cardSyn = Card();
        private readonly Chart _chart = new() { Dock = DockStyle.Fill };
        private readonly ListView _servers = NewList();
        private readonly ListView _incidents = NewList();

        public DashboardTab(ApiClient api)
        {
            _api = api;
            Dock = DockStyle.Fill;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));

            var cards = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(4) };
            AddCard(cards, _cardMbit, "Eingehend (MBit/s)");
            AddCard(cards, _cardPps, "Pakete/s");
            AddCard(cards, _cardConn, "Verbindungen");
            AddCard(cards, _cardSyn, "SYN-RECV");

            ConfigureChart();

            _servers.Columns.Add("Server", 170);
            _servers.Columns.Add("Zuletzt gemeldet", 150);
            _servers.Columns.Add("Eingehend", 110);
            _servers.Columns.Add("Status", 130);
            _incidents.Columns.Add("#", 45);
            _incidents.Columns.Add("Server", 130);
            _incidents.Columns.Add("Beginn", 140);
            _incidents.Columns.Add("Ende", 140);
            _incidents.Columns.Add("Status", 80);
            _incidents.Columns.Add("Spitze MBit/s", 100);

            layout.Controls.Add(_banner, 0, 0);
            layout.Controls.Add(cards, 0, 1);
            layout.Controls.Add(Group("Verlauf Hauptserver (letzte 60 Minuten)", _chart), 0, 2);
            layout.Controls.Add(Group("Überwachte Server", _servers), 0, 3);
            layout.Controls.Add(Group("Letzte Vorfälle", _incidents), 0, 4);
            Controls.Add(layout);
        }

        private static Label Card() => new()
        {
            Text = "–",
            Dock = DockStyle.Fill,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 18f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        private static void AddCard(FlowLayoutPanel host, Label value, string caption)
        {
            var panel = new Panel { Width = 190, Height = 70, Margin = new Padding(0, 0, 10, 0) };
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

        private void ConfigureChart()
        {
            var area = new ChartArea("main");
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisY.Title = "MBit/s";
            area.AxisY2.Title = "Verbindungen";
            area.AxisY2.MajorGrid.Enabled = false;
            _chart.ChartAreas.Add(area);
            _chart.Legends.Add(new Legend("legend") { Docking = Docking.Top });
            _chart.Series.Add(new Series("MBit/s eingehend") { ChartType = SeriesChartType.Line, ChartArea = "main", Color = Color.SteelBlue, BorderWidth = 2 });
            _chart.Series.Add(new Series("Verbindungen") { ChartType = SeriesChartType.Line, ChartArea = "main", Color = Color.DarkOrange, YAxisType = AxisType.Secondary, BorderWidth = 2 });
        }

        public async Task RefreshAsync()
        {
            try
            {
                var statusTask = _api.GetStatusAsync();
                var samplesTask = _api.GetSamplesAsync(60);
                var serversTask = _api.GetServersAsync();
                var incidentsTask = _api.GetIncidentsAsync(8);
                await Task.WhenAll(statusTask, samplesTask, serversTask, incidentsTask);

                var s = statusTask.Result.LatestSample;
                _cardMbit.Text = s is null ? "–" : s.MbitIn.ToString("N1");
                _cardPps.Text = s is null ? "–" : s.PpsIn.ToString("N0");
                _cardConn.Text = s is null ? "–" : s.TotalConn.ToString("N0");
                _cardSyn.Text = s is null ? "–" : s.SynRecv.ToString("N0");

                if (statusTask.Result.ActiveIncident is { } inc)
                {
                    _banner.Text = $"⚠️ Aktiver Vorfall #{inc.Id} auf {inc.ServerName} seit {inc.StartedAt} — {inc.TriggerReason}";
                    _banner.BackColor = Color.FromArgb(255, 226, 224);
                    _banner.ForeColor = Color.FromArgb(170, 30, 20);
                }
                else
                {
                    _banner.Text = s is null ? "Noch keine Messwerte vom Hauptserver." : "✅ Kein aktiver Vorfall — Netzwerk unauffällig.";
                    _banner.BackColor = Color.FromArgb(228, 248, 232);
                    _banner.ForeColor = Color.FromArgb(20, 110, 45);
                }

                _chart.Series[0].Points.Clear();
                _chart.Series[1].Points.Clear();
                foreach (var p in samplesTask.Result.Samples)
                {
                    var label = p.Ts.Length >= 19 ? p.Ts.Substring(11, 8) : p.Ts;
                    _chart.Series[0].Points.AddXY(label, p.MbitIn);
                    _chart.Series[1].Points.AddXY(label, p.TotalConn);
                }

                _servers.BeginUpdate();
                _servers.Items.Clear();
                foreach (var sv in serversTask.Result.Servers.Where(x => x.RevokedAt == null))
                {
                    var status = sv.ActiveIncidents > 0 ? "DDoS-Verdacht" : sv.Online ? "online" : sv.LastSeenAt == null ? "wartet auf Agent" : "keine Meldung";
                    var item = new ListViewItem(new[] { sv.Name, sv.LastSeenAt ?? "noch nie", sv.LastMbit is { } m ? m.ToString("N1") + " MBit/s" : "–", status });
                    item.ForeColor = sv.ActiveIncidents > 0 ? Color.Firebrick : sv.Online ? Color.SeaGreen : Color.DarkGoldenrod;
                    _servers.Items.Add(item);
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
    }
}
