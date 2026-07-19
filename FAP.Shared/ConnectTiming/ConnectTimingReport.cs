using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FAP.Shared.ConnectTiming
{
    /// <summary>
    /// Stable JSON schema for agent loops and cross-version compare scripts.
    /// </summary>
    public sealed class ConnectTimingReport
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("scenario")]
        public string Scenario { get; set; } = "";

        [JsonPropertyName("runtime")]
        public string Runtime { get; set; } = "net9";

        [JsonPropertyName("totalMs")]
        public long TotalMs { get; set; }

        [JsonPropertyName("budgetMs")]
        public long BudgetMs { get; set; }

        [JsonPropertyName("phases")]
        public Dictionary<string, long> Phases { get; set; } = new();

        /// <summary>Derived metrics for A/B compare (optional; agents may ignore).</summary>
        [JsonPropertyName("metrics")]
        public Dictionary<string, long> Metrics { get; set; } = new();

        /// <summary>Per-client breakdown for multi_cold_start (omitted for single-host scenarios).</summary>
        [JsonPropertyName("clients")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ConnectTimingClientReport>? Clients { get; set; }
    }

    public sealed class ConnectTimingClientReport
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = "";

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("elected")]
        public bool Elected { get; set; }

        [JsonPropertyName("totalMs")]
        public long TotalMs { get; set; }

        [JsonPropertyName("phases")]
        public Dictionary<string, long> Phases { get; set; } = new();

        [JsonPropertyName("metrics")]
        public Dictionary<string, long> Metrics { get; set; } = new();
    }
}
