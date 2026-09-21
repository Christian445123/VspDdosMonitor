using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VspDdosMonitor.Services
{
    /// <summary>
    /// Client für die REST-API des VSRP DDoS Monitor (public/api/index.php). Authentifizierung
    /// per API-Schlüssel (Header "Authorization: Bearer ..."), passend zu ApiAuth.php auf dem Server.
    /// </summary>
    public sealed class ApiClient
    {
        private readonly AppSettings _settings;
        private readonly HttpClient _http;

        public ApiClient(AppSettings settings)
        {
            _settings = settings;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VspDdosMonitor", "1.0"));
            if (!string.IsNullOrEmpty(settings.ApiKey))
            {
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            }
            if (!string.IsNullOrEmpty(settings.LicenseKey))
            {
                _http.DefaultRequestHeaders.Add("X-License-Key", settings.LicenseKey);
            }
        }

        private Uri BuildUri(string path)
        {
            var baseUrl = _settings.BaseUrl.TrimEnd('/');
            // Ohne URL-Umschreibung (Nginx liest keine .htaccess): Route als Query-Parameter an index.php übergeben
            var trimmed = path.TrimStart('/');
            var q = trimmed.IndexOf('?');
            var route = q >= 0 ? trimmed.Substring(0, q) : trimmed;
            var extra = q >= 0 ? "&" + trimmed.Substring(q + 1) : "";
            return new Uri(baseUrl + "/api/index.php?r=" + route + extra);
        }

        private async Task<T> GetAsync<T>(string path)
        {
            using var response = await _http.GetAsync(BuildUri(path)).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException(ExtractMessage(body, response.StatusCode.ToString()), (int)response.StatusCode);
            }
            var result = JsonConvert.DeserializeObject<T>(body);
            if (result == null) throw new ApiException("Leere Antwort vom Server.");
            return result;
        }

        private async Task<T> PostAsync<T>(string path, object body)
        {
            var json = JsonConvert.SerializeObject(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(BuildUri(path), content).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException(ExtractMessage(responseBody, response.StatusCode.ToString()), (int)response.StatusCode);
            }
            return JsonConvert.DeserializeObject<T>(responseBody) ?? throw new ApiException("Leere Antwort vom Server.");
        }

        private async Task PostAsync(string path, object body)
        {
            var json = JsonConvert.SerializeObject(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(BuildUri(path), content).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException(ExtractMessage(responseBody, response.StatusCode.ToString()), (int)response.StatusCode);
            }
        }

        private static string ExtractMessage(string body, string fallback)
        {
            try
            {
                var obj = JObject.Parse(body);
                var message = obj["message"]?.ToString();
                if (!string.IsNullOrEmpty(message)) return message!;
                var error = obj["error"]?.ToString();
                if (!string.IsNullOrEmpty(error)) return error!;
            }
            catch (Exception)
            {
                // ignorieren, Fallback verwenden
            }
            return "Serverfehler (" + fallback + ").";
        }

        public Task<PingResult> PingAsync() => GetAsync<PingResult>("ping");

        public Task<StatusResult> GetStatusAsync(int serverId = 0) => GetAsync<StatusResult>($"status?server_id={serverId}");

        public Task<IncidentListResult> GetIncidentsAsync(int limit = 50, int? serverId = null) =>
            GetAsync<IncidentListResult>($"incidents?limit={limit}" + (serverId.HasValue ? $"&server_id={serverId.Value}" : ""));

        public Task<IncidentDetailResult> GetIncidentAsync(int id) => GetAsync<IncidentDetailResult>($"incidents/{id}");

        public Task<SamplesResult> GetSamplesAsync(int minutes = 30, int serverId = 0) => GetAsync<SamplesResult>($"samples?minutes={minutes}&server_id={serverId}");

        public Task<ServersResult> GetServersAsync() => GetAsync<ServersResult>("servers");

        public Task<CreateServerResult> CreateServerAsync(string name) => PostAsync<CreateServerResult>("servers", new { name });

        public Task<MessageResult> RevokeServerAsync(int id) => PostAsync<MessageResult>($"servers/{id}/revoke", new { });

        public Task<SettingsResult> GetSettingsAsync() => GetAsync<SettingsResult>("settings");

        public Task<MessageResult> SaveThresholdsAsync(Dictionary<string, string> thresholds) =>
            PostAsync<MessageResult>("settings", new { thresholds });

        public Task<MessageResult> TestEmailAsync() => PostAsync<MessageResult>("settings/test-email", new { });

        public Task SetSuspectStatusAsync(int suspectId, string status) =>
            PostAsync($"suspects/{suspectId}/status", new { status });
    }

    public sealed class IncidentListResult
    {
        [JsonProperty("incidents")] public System.Collections.Generic.List<IncidentDto> Incidents { get; set; } = new();
    }

    public sealed class SamplesResult
    {
        [JsonProperty("samples")] public System.Collections.Generic.List<SampleDto> Samples { get; set; } = new();
    }
}
