using System.Collections.Generic;
using Newtonsoft.Json;

namespace VspDdosMonitor.Services
{
    public sealed class SampleDto
    {
        [JsonProperty("ts")] public string Ts { get; set; } = "";
        [JsonProperty("mbit_in")] public double MbitIn { get; set; }
        [JsonProperty("pps_in")] public long PpsIn { get; set; }
        [JsonProperty("mbit_out")] public double MbitOut { get; set; }
        [JsonProperty("pps_out")] public long PpsOut { get; set; }
        [JsonProperty("total_conn")] public long TotalConn { get; set; }
        [JsonProperty("syn_recv")] public long SynRecv { get; set; }
    }

    public sealed class IncidentDto
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("started_at")] public string StartedAt { get; set; } = "";
        [JsonProperty("resolved_at")] public string? ResolvedAt { get; set; }
        [JsonProperty("status")] public string Status { get; set; } = "";
        [JsonProperty("peak_mbit_in")] public double PeakMbitIn { get; set; }
        [JsonProperty("peak_pps_in")] public long PeakPpsIn { get; set; }
        [JsonProperty("peak_total_conn")] public long PeakTotalConn { get; set; }
        [JsonProperty("peak_syn_recv")] public long PeakSynRecv { get; set; }
        [JsonProperty("trigger_reason")] public string TriggerReason { get; set; } = "";
        [JsonProperty("server_name")] public string ServerName { get; set; } = "";
        [JsonProperty("notified_email")] public bool NotifiedEmail { get; set; }
        [JsonProperty("notified_discord")] public bool NotifiedDiscord { get; set; }
    }

    public sealed class SuspectDto
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("incident_id")] public int IncidentId { get; set; }
        [JsonProperty("ip")] public string Ip { get; set; } = "";
        [JsonProperty("conn_count")] public long ConnCount { get; set; }
        [JsonProperty("syn_recv_count")] public long SynRecvCount { get; set; }
        [JsonProperty("suggested_cmd_nft")] public string SuggestedCmdNft { get; set; } = "";
        [JsonProperty("suggested_cmd_iptables")] public string SuggestedCmdIptables { get; set; } = "";
        [JsonProperty("status")] public string Status { get; set; } = "";
    }

    public sealed class StatusResult
    {
        [JsonProperty("latest_sample")] public SampleDto? LatestSample { get; set; }
        [JsonProperty("active_incident")] public IncidentDto? ActiveIncident { get; set; }
    }

    public sealed class IncidentDetailResult
    {
        [JsonProperty("incident")] public IncidentDto Incident { get; set; } = new IncidentDto();
        [JsonProperty("suspects")] public List<SuspectDto> Suspects { get; set; } = new List<SuspectDto>();
    }

    public sealed class PingResult
    {
        [JsonProperty("ok")] public bool Ok { get; set; }
        [JsonProperty("key_label")] public string KeyLabel { get; set; } = "";
        [JsonProperty("server_time")] public string ServerTime { get; set; } = "";
    }
}
