using System;
using System.Text.Json.Serialization;

namespace Sufficit.Gateway.Zabbix
{
    /// <summary>
    /// User-supplied settings for validating or provisioning the Sufficit webhook in a customer Zabbix.
    /// </summary>
    public sealed class ZabbixAutomationRequest
    {
        /// <summary>
        /// Identifier of the saved Sufficit Zabbix integration to test or configure.
        /// The API uses it to load the tenant context, stored token and previously created remote object identifiers.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Public HTTPS address of the customer Zabbix installation.
        /// It may contain either the server root or the complete <c>/api_jsonrpc.php</c> path.
        /// When omitted, the automation reuses the address already stored for the integration.
        /// </summary>
        [JsonPropertyName("api_url")]
        public string? ApiUrl { get; set; }

        /// <summary>
        /// Zabbix API token used to validate access and provision media types, user media and actions.
        /// The clear value is accepted only as input, protected before persistence and never returned in
        /// <see cref="ZabbixAutomationStatus"/>. When omitted, the protected token already stored for the integration is reused.
        /// </summary>
        [JsonPropertyName("api_token")]
        public string? ApiToken { get; set; }

        /// <summary>
        /// Lowest Zabbix trigger severity that should produce a telephone alert action.
        /// Valid values are 0 through 5; the service normalizes out-of-range values and defaults to 3 (Average).
        /// </summary>
        [JsonPropertyName("minimum_severity")]
        public int MinimumSeverity { get; set; } = 3;
    }
}
