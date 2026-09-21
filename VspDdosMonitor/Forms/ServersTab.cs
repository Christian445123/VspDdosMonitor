using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using VspDdosMonitor.Services;

namespace VspDdosMonitor.Forms
{
    /// <summary>Überwachte Server verwalten: anlegen (liefert den Installationsbefehl für den Agenten) und widerrufen.</summary>
    public sealed class ServersTab : UserControl
    {
        private readonly ApiClient _api;
        private readonly ListView _list = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false };
        private readonly TextBox _name = new() { Width = 260 };

        public ServersTab(ApiClient api)
        {
            _api = api;
            Dock = DockStyle.Fill;

            _list.Columns.Add("Name", 170);
            _list.Columns.Add("Host", 190);
            _list.Columns.Add("Schlüssel", 90);
            _list.Columns.Add("Zuletzt gemeldet", 150);
            _list.Columns.Add("Status", 140);

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 84, Padding = new Padding(6) };
            top.Controls.Add(new Label { Text = "Neuer Server:", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
            top.Controls.Add(_name);
            var create = new Button { Text = "Server anlegen", Width = 130 };
            Theme.Primary(create);
            create.Click += async (_, _) => await CreateAsync();
            var revoke = new Button { Text = "Ausgewählten widerrufen", Width = 170 };
            revoke.Click += async (_, _) => await RevokeAsync();
            var refresh = new Button { Text = "Aktualisieren", Width = 110 };
            refresh.Click += async (_, _) => await RefreshAsync();
            top.Controls.Add(create);
            top.Controls.Add(revoke);
            top.Controls.Add(refresh);
            top.Controls.Add(new Label
            {
                Text = "Auf jedem Linux-Server läuft ein Agent, der Netzwerkwerte hierher meldet. Beim Anlegen erscheint der Installationsbefehl (für alle Server gleich, nur der Schlüssel unterscheidet sich).",
                AutoSize = false, Width = 760, Height = 32, ForeColor = Color.DimGray,
            });

            Controls.Add(_list);
            Controls.Add(top);
        }

        public async Task RefreshAsync()
        {
            try
            {
                var r = await _api.GetServersAsync();
                _list.BeginUpdate();
                _list.Items.Clear();
                foreach (var s in r.Servers)
                {
                    var status = s.RevokedAt != null ? "widerrufen" : s.ActiveIncidents > 0 ? "DDoS-Verdacht" : s.Online ? "online" : s.LastSeenAt == null ? "wartet auf Agent" : "keine Meldung";
                    var host = string.IsNullOrEmpty(s.Hostname) ? "–" : s.Hostname + (string.IsNullOrEmpty(s.LastIp) ? "" : " (" + s.LastIp + ")");
                    _list.Items.Add(new ListViewItem(new[] { s.Name, host, "vsrv_…" + s.TokenHint, s.LastSeenAt ?? "noch nie", status })
                    {
                        Tag = s.Id,
                        ForeColor = s.RevokedAt != null ? Theme.Muted : s.ActiveIncidents > 0 ? Theme.Danger : s.Online ? Theme.Success : Theme.Warn,
                    });
                }
                _list.EndUpdate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Server konnten nicht geladen werden:\n" + ex.Message, "VSRP DDoS Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task CreateAsync()
        {
            var name = _name.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, "Bitte einen Namen für den Server eingeben.", "VSRP DDoS Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                var r = await _api.CreateServerAsync(name);
                _name.Text = "";
                using (var dlg = new InstallCommandForm(r.Name, r.InstallCommand)) dlg.ShowDialog(this);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Fehler: " + ex.Message, "VSRP DDoS Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task RevokeAsync()
        {
            if (_list.SelectedItems.Count == 0) return;
            var item = _list.SelectedItems[0];
            if (MessageBox.Show(this, $"Schlüssel von „{item.Text}“ wirklich widerrufen? Der Agent wird danach abgelehnt.", "Widerrufen",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                await _api.RevokeServerAsync((int)item.Tag!);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Fehler: " + ex.Message, "VSRP DDoS Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    /// <summary>Zeigt den Installationsbefehl mit Server-Schlüssel (nur jetzt sichtbar) zum Kopieren.</summary>
    public sealed class InstallCommandForm : Form
    {
        public InstallCommandForm(string serverName, string command)
        {
            Text = "Server angelegt – Installationsbefehl";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(640, 190);

            var info = new Label
            {
                Left = 14, Top = 12, Width = 612, Height = 40,
                Text = $"Server „{serverName}“ angelegt. Der Schlüssel wird nur jetzt angezeigt. Diesen Befehl auf dem Linux-Server als root ausführen:",
            };
            var box = new TextBox { Left = 14, Top = 58, Width = 612, Height = 70, Multiline = true, ReadOnly = true, Text = command, Font = new Font("Consolas", 9f) };
            var copy = new Button { Left = 14, Top = 140, Width = 150, Height = 30, Text = "In Zwischenablage" };
            copy.Click += (_, _) => { try { Clipboard.SetText(command); } catch (Exception) { } };
            var close = new Button { Left = 526, Top = 140, Width = 100, Height = 30, Text = "Schließen", DialogResult = DialogResult.OK };
            Controls.AddRange(new Control[] { info, box, copy, close });
            AcceptButton = close;
            Theme.Primary(close);
            Theme.Apply(this);
        }
    }
}
