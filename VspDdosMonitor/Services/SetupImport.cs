using System;
using Microsoft.Win32;

namespace VspDdosMonitor.Services
{
    /// <summary>
    /// Übernimmt die im Installer eingegebenen Zugangsdaten (API-Adresse, API-Schlüssel).
    /// Der Installer legt sie unter HKLM\Software\ViennaStateRP\VspDdosMonitor\Setup ab; die Anwendung
    /// kopiert sie beim Start in ihre Einstellungen (dort pro Benutzer mit Windows-DPAPI verschlüsselt)
    /// und entfernt danach den Schlüssel aus der Registry, soweit ihre Rechte dafür reichen.
    /// </summary>
    public static class SetupImport
    {
        public const string DefaultKeyPath = @"SOFTWARE\ViennaStateRP\VspDdosMonitor\Setup";

        /// <returns>true, wenn neue Werte aus dem Installer übernommen wurden</returns>
        public static bool Apply(AppSettings s, RegistryKey? baseKey = null, string keyPath = DefaultKeyPath)
        {
            try
            {
                var ownsBase = baseKey is null;
                baseKey ??= RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                try
                {
                    using var key = baseKey.OpenSubKey(keyPath, writable: false);
                    if (key is null) return false;

                    var stamp = key.GetValue("Stamp") as string ?? "";
                    var apiKey = key.GetValue("ApiKey") as string ?? "";
                    var license = key.GetValue("LicenseKey") as string ?? "";

                    if (stamp.Length == 0 || stamp == s.SetupStamp) return false;
                    if (apiKey.Length == 0 && license.Length == 0) return false;

                    if (apiKey.Length > 0) s.ApiKey = apiKey.Trim();
                    if (license.Length > 0) s.LicenseKey = license.Trim().ToUpperInvariant();
                    s.SetupStamp = stamp;
                    s.Save();

                    try
                    {
                        using var writable = baseKey.OpenSubKey(keyPath, writable: true);
                        writable?.DeleteValue("ApiKey", throwOnMissingValue: false);
                        writable?.DeleteValue("LicenseKey", throwOnMissingValue: false);
                    }
                    catch (Exception)
                    {
                    }
                    return true;
                }
                finally
                {
                    if (ownsBase) baseKey.Dispose();
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
