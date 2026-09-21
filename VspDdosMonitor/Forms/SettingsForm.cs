using System;
using System.Drawing;
using System.Windows.Forms;
using VspDdosMonitor.Services;

namespace VspDdosMonitor.Forms
{
    public sealed class SettingsForm : Form
    {
        private readonly AppSettings _settings;

        private readonly TextBox _apiKey = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        private readonly CheckBox _autoCheck = new() { Dock = DockStyle.Fill, Text = "Beim Start automatisch nach Updates suchen", AutoSize = true };
        private readonly Label _status = new() { Dock = DockStyle.Fill, AutoSize = false, ForeColor = Color.DarkRed, TextAlign = ContentAlignment.MiddleLeft };

        public SettingsForm(AppSettings settings, bool firstRun)
        {
            _settings = settings;

            Text = "VSRP DDoS Monitor – Einstellungen";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(440, 340);
            Padding = new Padding(16);

            _apiKey.Text = settings.ApiKey;
            _autoCheck.Checked = settings.AutoCheckUpdates;

            var intro = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 50,
                Text = "API-Schlüssel des VSRP DDoS Monitor eintragen. Den Schlüssel erstellt ein " +
                       "Administrator im Web-Dashboard unter „API-Zugang“.",
                ForeColor = Color.DimGray,
            };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                AutoSize = true,
            };
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            table.Controls.Add(FieldLabel("API-Schlüssel"));
            table.Controls.Add(_apiKey);
            table.Controls.Add(_autoCheck);
            table.Controls.Add(_status);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 40,
                AutoSize = false,
            };
            var saveButton = new Button { Text = "Speichern", DialogResult = DialogResult.OK, Width = 100 };
            saveButton.Click += SaveButton_Click;
            var cancelButton = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Width = 100, Visible = !firstRun };
            var testButton = new Button { Text = "Verbindung testen", Width = 140 };
            testButton.Click += TestButton_Click;
            buttonPanel.Controls.Add(saveButton);
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(testButton);

            AcceptButton = saveButton;
            CancelButton = firstRun ? null : cancelButton;

            Controls.Add(table);
            Controls.Add(intro);
            Controls.Add(buttonPanel);
        }

        private static Label FieldLabel(string text) => new()
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = text,
            ForeColor = Color.DimGray,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 8f),
        };

        private async void TestButton_Click(object? sender, EventArgs e)
        {
            _status.ForeColor = Color.DarkRed;
            _status.Text = "Teste Verbindung ...";
            try
            {
                var temp = new AppSettings { ApiKey = _apiKey.Text.Trim() };
                var api = new ApiClient(temp);
                var result = await api.PingAsync();
                _status.ForeColor = Color.DarkGreen;
                _status.Text = "Verbindung erfolgreich (Schlüssel: " + result.KeyLabel + ").";
            }
            catch (Exception ex)
            {
                _status.ForeColor = Color.DarkRed;
                _status.Text = "Fehler: " + ex.Message;
            }
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            var apiKey = _apiKey.Text.Trim();
            if (apiKey.Length == 0)
            {
                _status.ForeColor = Color.DarkRed;
                _status.Text = "Bitte den API-Schlüssel eintragen.";
                DialogResult = DialogResult.None;
                return;
            }

            _settings.ApiKey = apiKey;
            _settings.AutoCheckUpdates = _autoCheck.Checked;
            _settings.Save();
        }
    }
}
