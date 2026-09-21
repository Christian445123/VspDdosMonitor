using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace VspDdosMonitor.Services
{
    /// <summary>
    /// Verbindungseinstellungen: Adresse der Web-API und API-Schlüssel. Schlüssel werden mit
    /// Windows-DPAPI (nur für den aktuellen Windows-Benutzer lesbar) verschlüsselt gespeichert.
    /// </summary>
    public sealed class AppSettings
    {
        public const string DefaultBaseUrl = "https://ddos.viennastaterp.at";

        /// <summary>Adresse der Web-Anwendung (fest hinterlegt, nicht vom Benutzer änderbar).</summary>
        [JsonIgnore]
        public string BaseUrl => DefaultBaseUrl;

        public string ApiKeyProtected { get; set; } = "";

        [JsonIgnore]
        public string ApiKey
        {
            get => Unprotect(ApiKeyProtected);
            set => ApiKeyProtected = Protect(value);
        }

        // ── Programm-Updates (GitHub Releases) ───────────────────────────────

        /// <summary>GitHub-Repository der Anwendung im Format "Besitzer/Repository".</summary>
        public string GitHubRepo { get; set; } = "Christian445123/VspDdosMonitor";

        /// <summary>Beim Start automatisch nach einer neuen Version suchen.</summary>
        public bool AutoCheckUpdates { get; set; } = true;

        public string GitHubTokenProtected { get; set; } = "";

        /// <summary>Zeitstempel der zuletzt übernommenen Installer-Eingaben (siehe SetupImport).</summary>
        public string SetupStamp { get; set; } = "";

        /// <summary>Nur bei privatem Repository nötig (Lesezugriff auf Releases).</summary>
        [JsonIgnore]
        public string GitHubToken
        {
            get => Unprotect(GitHubTokenProtected);
            set => GitHubTokenProtected = Protect(value);
        }

        [JsonIgnore]
        public bool IsConfigured => !string.IsNullOrEmpty(ApiKey);

        /// <summary>Nur für Tests: anderer Speicherort statt %APPDATA%.</summary>
        public static string? PathOverride { get; set; }

        private static string FilePath => PathOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VspDdosMonitor", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var settings = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(FilePath));
                    if (settings != null) return settings;
                }
            }
            catch (Exception)
            {
                // defekte Datei -> Standardwerte
            }
            return new AppSettings();
        }

        public void Save()
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        private static string Protect(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }

        private static string Unprotect(string protectedValue)
        {
            if (string.IsNullOrEmpty(protectedValue)) return "";
            try
            {
                var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedValue), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
