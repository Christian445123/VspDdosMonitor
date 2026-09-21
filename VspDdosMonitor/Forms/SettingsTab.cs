using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using VspDdosMonitor.Services;

namespace VspDdosMonitor.Forms
{
    /// <summary>Erkennungs-Schwellwerte ändern und Status der Benachrichtigungen (E-Mail/Discord) ansehen, Test-E-Mail senden.</summary>
    public sealed class SettingsTab : UserControl
    {
        private static readonly (string Key, string Label)[] Fields =
        {
            ("mbit_threshold", "Bandbreite-Schwelle (MBit/s eingehend)"),
            ("pps_threshold", "Pakete/s-Schwelle"),
            ("total_conn_threshold", "Verbindungen gesamt – Schwelle"),
            ("syn_recv_threshold", "SYN-RECV – Schwelle"),
            ("per_ip_conn_threshold", "Verbindungen je Einzel-IP – Schwelle"),
            ("sample_interval_seconds", "Messintervall (Sekunden)"),
            ("consecutive_to_trigger", "Aufeinanderfolgende Messungen bis Alarm"),
            ("consecutive_to_resolve", "Aufeinanderfolgende Messungen bis Entwarnung"),
            ("monitor_interface", "Netzwerkschnittstelle des Hauptservers (z. B. eth0)"),
            ("samples_retention_days", "Messwerte aufbewahren (Tage)"),
        };

        private readonly ApiClient _api;
        private readonly Dictionary<string, TextBox> _boxes = new();
        private readonly Label _notifications = new() { Dock = DockStyle.Fill, ForeColor = Color.DimGray };
        private readonly Label _status = new() { AutoSize = true, Margin = new Padding(12, 8, 0, 0) };

        public SettingsTab(ApiClient api)
        {
            _api = api;
            Dock = DockStyle.Fill;

            var thresholds = new GroupBox { Text = "Schwellwerte für die Erkennung (gelten für alle Server)", Dock = DockStyle.Top, Height = 340 };
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            foreach (var (key, label) in Fields)
            {
                var box = new TextBox { Width = 150 };
                _boxes[key] = box;
                grid.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 6, 0, 0) });
                grid.Controls.Add(box);
            }
            thresholds.Controls.Add(grid);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
            var save = new Button { Text = "Speichern", Width = 110 };
            save.Click += async (_, _) => await SaveAsync();
            var reload = new Button { Text = "Neu laden", Width = 100 };
            reload.Click += async (_, _) => await RefreshAsync();
            buttons.Controls.Add(save);
            buttons.Controls.Add(reload);
            buttons.Controls.Add(_status);

            var notifBox = new GroupBox { Text = "Benachrichtigungen", Dock = DockStyle.Fill };
            var test = new Button { Text = "Test-E-Mail senden", Dock = DockStyle.Bottom, Height = 30 };
            test.Click += async (_, _) => await TestEmailAsync();
            notifBox.Controls.Add(_notifications);
            notifBox.Controls.Add(test);

            Controls.Add(notifBox);
            Controls.Add(buttons);
            Controls.Add(thresholds);
        }

        public async Task RefreshAsync()
        {
            try
            {
                var s = await _api.GetSettingsAsync();
                foreach (var (key, _) in Fields)
                {
                    _boxes[key].Text = s.Thresholds.TryGetValue(key, out var v) ? v : "";
                }
                var n = s.Notifications;
                string Or(string v) => string.IsNullOrEmpty(v) ? "– nicht gesetzt –" : v;
                _notifications.Text =
                    "SMTP-Zugangsdaten und Discord-Webhook stehen aus Sicherheitsgründen nur in der .env auf dem Server\r\n" +
                    "(nicht in der Datenbank) und werden dort geändert.\r\n\r\n" +
                    $"SMTP-Host: {Or(n.SmtpHost)}    Absender: {Or(n.FromEmail)}\r\n" +
                    $"Alarm-E-Mail an: {Or(n.AlertTo)}    Discord-Webhook: {(n.DiscordSet ? "gesetzt" : "nicht gesetzt")}";
            }
            catch (Exception ex)
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = "Fehler: " + ex.Message;
            }
        }

        private async Task SaveAsync()
        {
            var values = new Dictionary<string, string>();
            foreach (var (key, _) in Fields) values[key] = _boxes[key].Text.Trim();
            try
            {
                await _api.SaveThresholdsAsync(values);
                _status.ForeColor = Color.SeaGreen;
                _status.Text = "Gespeichert. Der Collector übernimmt die Werte beim nächsten Messzyklus.";
            }
            catch (Exception ex)
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = "Fehler: " + ex.Message;
            }
        }

        private async Task TestEmailAsync()
        {
            try
            {
                var r = await _api.TestEmailAsync();
                MessageBox.Show(this, r.Message, "Test-E-Mail", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Test-E-Mail", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
