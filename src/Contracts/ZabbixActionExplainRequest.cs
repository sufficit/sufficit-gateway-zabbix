using System;
using System.Text.Json.Serialization;

namespace Sufficit.Gateway.Zabbix
{
    /// <summary>
    /// Read-only request to describe an existing action in the customer's Zabbix, by name.
    /// Never mutates anything; see <see cref="ZabbixAutomationService.ExplainActionsAsync"/>.
    /// </summary>
    public sealed class ZabbixActionExplainRequest
    {
        /// <summary>
        /// Identifier of the saved Sufficit Zabbix integration whose stored URL and token are reused.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Exact Zabbix action name to look up. Null or empty returns every action visible to the token.
        /// </summary>
        [JsonPropertyName("action_name")]
        public string? ActionName { get; set; }
    }
}
