using System;
using System.Text.Json.Serialization;

namespace Sufficit.Gateway.Zabbix
{
    /// <summary>
    /// Filters used to search configured Zabbix gateway integrations.
    /// Null properties do not restrict the query.
    /// </summary>
    public class ZabbixGatewaySearchParameters : ILimit
    {
        /// <summary>
        /// Optional exact filter by integration identifier.
        /// </summary>
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public Guid? Id { get; set; }

        /// <summary>
        /// Optional exact filter by tenant context identifier.
        /// Use it to keep integration listings scoped to the currently authorized customer.
        /// </summary>
        [JsonPropertyName("contextid")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public Guid? ContextId { get; set; }

        /// <summary>
        /// Optional text filter applied to the integration title.
        /// <see cref="TextFilter.ExactMatch"/> controls whether matching is exact or contains-based.
        /// </summary>
        [JsonPropertyName("title")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public TextFilter? Title { get; set; }

        /// <summary>
        /// Optional filter by operational state.
        /// True returns active integrations; false returns paused integrations.
        /// </summary>
        [JsonPropertyName("enabled")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public bool? Enabled { get; set; }

        /// <summary>
        /// Maximum number of integrations returned after deterministic ordering by identifier.
        /// Null or zero leaves the provider query without an explicit row limit.
        /// </summary>
        [JsonPropertyName("limit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public uint? Limit { get; set; }
    }
}
