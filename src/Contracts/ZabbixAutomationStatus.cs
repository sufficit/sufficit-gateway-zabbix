using System;
using System.Text.Json.Serialization;

namespace Sufficit.Gateway.Zabbix
{
    /// <summary>
    /// Safe, token-free status returned by the Zabbix guided automation endpoints.
    /// </summary>
    public sealed class ZabbixAutomationStatus
    {
        /// <summary>
        /// Identifier of the Sufficit Zabbix integration represented by this status.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Normalized customer Zabbix JSON-RPC endpoint saved for this integration.
        /// This value never contains the API token.
        /// </summary>
        [JsonPropertyName("api_url")]
        public string? ApiUrl { get; set; }

        /// <summary>
        /// Indicates that a protected API token exists in persistence.
        /// It does not indicate that the token is currently valid; use <see cref="Connected"/> after a connection test.
        /// </summary>
        [JsonPropertyName("has_stored_token")]
        public bool HasStoredToken { get; set; }

        /// <summary>
        /// Indicates that the latest request successfully reached Zabbix and authenticated the token.
        /// Stored status reads return false until a new connection test is performed.
        /// </summary>
        [JsonPropertyName("connected")]
        public bool Connected { get; set; }

        /// <summary>
        /// Indicates that identifiers for both the managed Zabbix media type and action are stored.
        /// This reflects completed provisioning, but does not replace a fresh connection test.
        /// </summary>
        [JsonPropertyName("configured")]
        public bool Configured { get; set; }

        /// <summary>
        /// Indicates that the authenticated Zabbix user has permission to run automatic provisioning.
        /// Media type creation currently requires a Zabbix Super Admin token.
        /// </summary>
        [JsonPropertyName("can_configure")]
        public bool CanConfigure { get; set; }

        /// <summary>
        /// Zabbix API version reported by <c>apiinfo.version</c> during the latest successful test.
        /// </summary>
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        /// <summary>
        /// Display name of the Zabbix user that owns the authenticated API token.
        /// The automation attaches the managed media type to this user.
        /// </summary>
        [JsonPropertyName("user_name")]
        public string? UserName { get; set; }

        /// <summary>
        /// Lowest Zabbix trigger severity configured for the managed action, from 0 through 5.
        /// </summary>
        [JsonPropertyName("minimum_severity")]
        public int MinimumSeverity { get; set; }

        /// <summary>
        /// Identifier of the media type created or adopted by the Sufficit automation in the customer Zabbix.
        /// Null means no managed media type has been recorded.
        /// </summary>
        [JsonPropertyName("media_type_id")]
        public string? MediaTypeId { get; set; }

        /// <summary>
        /// Identifier of the trigger action created or adopted by the Sufficit automation in the customer Zabbix.
        /// Null means no managed action has been recorded.
        /// </summary>
        [JsonPropertyName("action_id")]
        public string? ActionId { get; set; }

        /// <summary>
        /// UTC instant when automatic provisioning most recently completed successfully.
        /// Null means provisioning has not completed.
        /// </summary>
        [JsonPropertyName("last_configured_at_utc")]
        public DateTime? LastConfiguredAtUtc { get; set; }

        /// <summary>
        /// Stable localization key from <see cref="ZabbixGatewayMessageCodes"/>.
        /// Frontends should select their user-facing text from this code.
        /// </summary>
        [JsonPropertyName("message_code")]
        public string? MessageCode { get; set; }

        /// <summary>
        /// English technical fallback for the current connection or provisioning state.
        /// Frontends should localize <see cref="MessageCode"/> instead of displaying this value when a translation exists.
        /// This value contains no clear token or protected credential material.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
