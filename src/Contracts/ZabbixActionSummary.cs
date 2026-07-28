using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sufficit.Gateway.Zabbix
{
    /// <summary>
    /// One condition of an action's filter, as read from the customer's Zabbix.
    /// Type and operator are returned as Zabbix's own raw integer codes: their meaning is
    /// documented by Zabbix and version-dependent, so this contract does not guess a label for
    /// them beyond what is stable across supported versions (see <see cref="ZabbixActionSummary.EvaluationTypeLabel"/>).
    /// </summary>
    public sealed class ZabbixActionConditionSummary
    {
        [JsonPropertyName("condition_type")]
        public int ConditionType { get; set; }

        [JsonPropertyName("operator")]
        public int Operator { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }

    /// <summary>
    /// Read-only, token-free description of one action in the customer's Zabbix, returned by
    /// <see cref="ZabbixAutomationService.ExplainActionsAsync"/>. Used to explain configuration
    /// the customer already has, such as why an action with two conditions might never fire.
    /// </summary>
    public sealed class ZabbixActionSummary
    {
        [JsonPropertyName("action_id")]
        public string? ActionId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        /// <summary>Zabbix's <c>filter.evaltype</c>: 0 And/Or, 1 And, 2 Or, 3 Custom expression.</summary>
        [JsonPropertyName("evaluation_type")]
        public int EvaluationType { get; set; }

        /// <summary>
        /// Stable, human-readable explanation of <see cref="EvaluationType"/>. Unlike condition
        /// type/operator codes, these four evaltype values are a long-standing, well-documented
        /// part of the Zabbix action API and safe to describe directly.
        /// </summary>
        [JsonPropertyName("evaluation_type_label")]
        public string? EvaluationTypeLabel { get; set; }

        [JsonPropertyName("formula")]
        public string? Formula { get; set; }

        [JsonPropertyName("conditions")]
        public List<ZabbixActionConditionSummary> Conditions { get; set; } = new();
    }
}
