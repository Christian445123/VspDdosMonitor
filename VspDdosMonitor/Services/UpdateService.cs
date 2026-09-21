using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VspDdosMonitor.Services
{
    /// <summary>Eine auf GitHub veröffentlichte, neuere Version.</summary>
    public sealed class UpdateInfo
    {
        public Version Version { get; }
        public string Tag { get; }
        public string Notes { get; }
        public string AssetName { get; }
        public string AssetUrl { get; }
        public string DownloadUrl { get; }
        public long Size { get; }
        public string ReleaseUrl { get; }

        public UpdateInfo(Version version, string tag, string notes, string assetName, string assetUrl, string downloadUrl, long size, string releaseUrl)
        {
            Version = version;
            Tag = tag;
            Notes = notes;
            AssetName = assetName;
            AssetUrl = assetUrl;
            DownloadUrl = downloadUrl;
            Size = size;
            ReleaseUrl = releaseUrl;
        }
    }

    /// <summary>
    /// Programm-Updates über GitHub Releases: sucht das neueste Release, vergleicht die Version
    /// (Tag "v1.1.0") mit der laufenden, lädt die .msi herunter und startet die Installation.
    /// Funktioniert mit öffentlichen Repositories ohne Anmeldung und mit privaten über einen Zugriffstoken.
    /// </summary>
    public static class UpdateService
    {
        private static readonly HttpClient Http = CreateClient();

        public static Version? LatestVersion { get; private set; }

        private static HttpClient CreateClient()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VspDdosMonitor", CurrentVersion.ToString()));
            return http;
        }

        /// <summary>Version der laufenden Anwendung (Major.Minor.Build).</summary>
        public static Version CurrentVersion
        {
            get
            {
                var v = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
                return Normalize(v);
            }
        }

        public static Version Normalize(Version v) => new Version(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));

        /// <summary>"v1.1.0" / "1.1.0-beta" -> 1.1.0, sonst null.</summary>
        public static Version? ParseTag(string tag)
        {
            var m = Regex.Match(tag ?? "", @"(\d+)\.(\d+)(?:\.(\d+))?");
            if (!m.Success) return null;
            return new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0);
        }

        private static HttpRequestMessage Request(string url, string token, string accept)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Accept.ParseAdd(accept);
            req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            if (!string.IsNullOrWhiteSpace(token))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
            }
            return req;
        }

        /// <summary>
        /// Liefert die neuere Version oder null, wenn die laufende aktuell ist.
        /// </summary>
        /// <exception cref="InvalidOperationException">mit verständlicher Meldung bei Fehlern</exception>
        public static async Task<UpdateInfo?> CheckAsync(string repo, string token, CancellationToken ct = default)
        {
            repo = (repo ?? "").Trim();
            if (!Regex.IsMatch(repo, @"^[\w.\-]+/[\w.\-]+$"))
            {
                throw new InvalidOperationException("Das Repository muss im Format „Besitzer/Repository“ angegeben werden.");
            }

            HttpResponseMessage response;
            try
            {
                using var req = Request($"https://api.github.com/repos/{repo}/releases/latest", token, "application/vnd.github+json");
                response = await Http.SendAsync(req, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException("GitHub ist nicht erreichbar (" + ex.Message + "). Bitte die Internetverbindung prüfen.");
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    string notFound = "";
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        var repoVisible = false;
                        try
                        {
                            using var repoReq = Request($"https://api.github.com/repos/{repo}", token, "application/vnd.github+json");
                            using var repoResp = await Http.SendAsync(repoReq, ct).ConfigureAwait(false);
                            repoVisible = repoResp.IsSuccessStatusCode;
                        }
                        catch (HttpRequestException)
                        {
                        }
                        notFound = repoVisible
                            ? $"Für das Repository „{repo}“ wurde noch kein Release veröffentlicht. Das Update-Angebot erscheint, sobald auf GitHub unter „Releases“ ein Release mit einer .msi-Datei und einem Tag wie v{CurrentVersion.Major}.{CurrentVersion.Minor + 1}.0 existiert."
                            : $"Das Repository „{repo}“ ist für die Anwendung nicht sichtbar. Bitte den Namen prüfen (Besitzer/Repository); ist es privat, muss unten ein GitHub-Zugriffstoken eingetragen werden.";
                    }

                    string message;
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.NotFound: message = notFound; break;
                        case HttpStatusCode.Unauthorized: message = "Der GitHub-Zugriffstoken ist ungültig oder abgelaufen."; break;
                        case HttpStatusCode.Forbidden: message = "GitHub verweigert den Zugriff (Abfragelimit erreicht oder Token ohne Berechtigung). Bitte später erneut versuchen."; break;
                        default: message = $"GitHub-Fehler {(int)response.StatusCode}."; break;
                    }
                    throw new InvalidOperationException(message);
                }

                var root = JObject.Parse(body);
                var tag = root.Value<string>("tag_name") ?? "";
                var version = ParseTag(tag);
                if (version is null)
                {
                    throw new InvalidOperationException($"Der Release-Name „{tag}“ enthält keine Versionsnummer (erwartet z. B. v1.1.0).");
                }
                LatestVersion = version;
                if (version <= CurrentVersion)
                {
                    return null;
                }

                // Installationsdatei: die .msi (bei mehreren die x64-Variante)
                JObject? best = null;
                if (root["assets"] is JArray assets)
                {
                    foreach (var a in assets)
                    {
                        var obj = (JObject)a;
                        var name = obj.Value<string>("name") ?? "";
                        if (!name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) continue;
                        if (best is null || name.IndexOf("x64", StringComparison.OrdinalIgnoreCase) >= 0) best = obj;
                    }
                }
                if (best is null)
                {
                    throw new InvalidOperationException($"Das Release {tag} enthält keine Installationsdatei (.msi).");
                }

                return new UpdateInfo(
                    version,
                    tag,
                    root.Value<string>("body") ?? "",
                    best.Value<string>("name") ?? "update.msi",
                    best.Value<string>("url") ?? "",
                    best.Value<string>("browser_download_url") ?? "",
                    best.Value<long?>("size") ?? 0,
                    root.Value<string>("html_url") ?? "");
            }
        }

        /// <summary>Lädt die Installationsdatei in den Temp-Ordner und liefert den Pfad.</summary>
        public static async Task<string> DownloadAsync(UpdateInfo info, string token, IProgress<int>? progress = null, CancellationToken ct = default)
        {
            var dir = Path.Combine(Path.GetTempPath(), "VspDdosMonitorUpdate");
            Directory.CreateDirectory(dir);
            var target = Path.Combine(dir, info.AssetName);

            var useApi = !string.IsNullOrWhiteSpace(token);
            using var req = Request(useApi ? info.AssetUrl : info.DownloadUrl, token, "application/octet-stream");
            using var response = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Der Download ist fehlgeschlagen (GitHub-Fehler {(int)response.StatusCode}).");
            }

            var total = response.Content.Headers.ContentLength ?? info.Size;
            using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var file = File.Create(target))
            {
                var buffer = new byte[81920];
                long done = 0;
                int read;
                var lastPercent = -1;
                while ((read = await source.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                {
                    await file.WriteAsync(buffer, 0, read, ct).ConfigureAwait(false);
                    done += read;
                    if (total > 0 && progress is not null)
                    {
                        var percent = (int)(done * 100 / total);
                        if (percent != lastPercent) { lastPercent = percent; progress.Report(percent); }
                    }
                }
            }

            if (info.Size > 0 && new FileInfo(target).Length != info.Size)
            {
                File.Delete(target);
                throw new InvalidOperationException("Die heruntergeladene Datei ist unvollständig. Bitte erneut versuchen.");
            }
            return target;
        }

        /// <summary>
        /// Startet die Installation und beendet danach die Anwendung. Ein kleines PowerShell-Skript zeigt ein Fenster mit
        /// Fortschrittsbalken, wartet bis die Anwendung geschlossen ist, installiert die .msi still im Hintergrund
        /// (Windows fragt höchstens einmal nach Administratorrechten), und startet das Programm automatisch neu.
        /// </summary>
        public static void InstallAndExit(string msiPath)
        {
            var installedExe = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "VSRP-DDoS-Monitor", "VspDdosMonitor.exe");
            var running = Process.GetCurrentProcess().MainModule?.FileName;
            var installedDir = Path.GetDirectoryName(installedExe)!;
            var relaunch = running is not null && running.StartsWith(installedDir, StringComparison.OrdinalIgnoreCase)
                ? running
                : installedExe;

            string Q(string s) => s.Replace("'", "''");
            var script = @"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()
$form = New-Object System.Windows.Forms.Form
$form.Text = 'VSRP DDoS Monitor - Update'
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedDialog'
$form.ControlBox = $false
$form.TopMost = $true
$form.ClientSize = New-Object System.Drawing.Size(460, 130)
$form.Font = New-Object System.Drawing.Font('Segoe UI', 10)
$label = New-Object System.Windows.Forms.Label
$label.Location = New-Object System.Drawing.Point(20, 18)
$label.Size = New-Object System.Drawing.Size(420, 44)
$label.Text = 'Update wird installiert ...'
$bar = New-Object System.Windows.Forms.ProgressBar
$bar.Location = New-Object System.Drawing.Point(20, 74)
$bar.Size = New-Object System.Drawing.Size(420, 22)
$bar.Style = 'Marquee'
$bar.MarqueeAnimationSpeed = 30
$form.Controls.Add($label)
$form.Controls.Add($bar)
$form.Show()
[System.Windows.Forms.Application]::DoEvents()
function Wait-Ui([int]$ms) {
  $end = [DateTime]::Now.AddMilliseconds($ms)
  while ([DateTime]::Now -lt $end) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 50 }
}
Wait-Ui 2500
$ok = $false
try {
  $p = Start-Process msiexec.exe -ArgumentList @('/i', '""__MSI__""', '/qn', '/norestart', '/l*v', '""__LOG__""') -Verb RunAs -PassThru
  while (-not $p.WaitForExit(100)) { [System.Windows.Forms.Application]::DoEvents() }
  $ok = ($p.ExitCode -eq 0 -or $p.ExitCode -eq 3010)
} catch { }
$bar.Style = 'Continuous'
if ($ok) {
  $bar.Value = 100
  $label.Text = 'Update abgeschlossen. Das Programm wird neu gestartet ...'
  Wait-Ui 2500
} else {
  $bar.Value = 0
  $label.Text = 'Das Update konnte nicht installiert werden. Die bisherige Version wird gestartet.'
  Wait-Ui 4500
}
if (Test-Path -LiteralPath '__EXE__') { Start-Process -FilePath '__EXE__' -ArgumentList '--restart' }
$form.Close()
"
                .Replace("__MSI__", msiPath.Replace("'", "''"))
                .Replace("__LOG__", Path.Combine(Path.GetDirectoryName(msiPath)!, "install.log"))
                .Replace("__EXE__", Q(relaunch));
            var scriptPath = Path.Combine(Path.GetDirectoryName(msiPath)!, "install-update.ps1");
            File.WriteAllText(scriptPath, script, new UTF8Encoding(true));

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            Environment.Exit(0);
        }
    }
}
