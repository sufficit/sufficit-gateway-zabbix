using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Sufficit.Gateway.Zabbix
{
    /// <summary>
    /// Complete configuration of a tenant Zabbix-to-telephone gateway.
    /// The public properties cover alert behavior, telephony routing and protected automation metadata.
    /// </summary>
    public class ZabbixGatewayIntegration
    {
        /// <summary>
        /// Lowest DTMF confirmation digit accepted by the gateway.
        /// </summary>
        public const uint MinimumDigit = 0;

        /// <summary>
        /// Highest DTMF confirmation digit accepted by the gateway.
        /// </summary>
        public const uint MaximumDigit = 9;

        /// <summary>
        /// Unique identifier of the integration.
        /// It is also embedded in the public webhook callback URL.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Server-computed public callback URL that the customer must configure in Zabbix.
        /// Clients must treat this value as read-only; it is derived from the API host configuration
        /// and is never persisted with the integration.
        /// </summary>
        [NotMapped]
        [JsonPropertyName("alert_callback_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public string? AlertCallbackUrl { get; set; }

        /// <summary>
        /// Tenant context that owns this integration and its destinations, executions and attempts.
        /// API operations must authorize access against this value.
        /// </summary>
        [JsonPropertyName("contextid")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Guid ContextId { get; set; }

        /// <summary>
        /// Human-readable integration name shown to administrators.
        /// Use a label that identifies the monitored environment or duty route.
        /// </summary>
        [JsonPropertyName("title")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public string? Title { get; set; }

        /// <summary>
        /// Determines whether new incoming Zabbix events may start alert processing.
        /// Disabling the integration preserves its configuration and history.
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Policy applied when the same normalized problem repeatedly enters the gateway.
        /// See <see cref="ZabbixFlapMode"/> for the available behaviors.
        /// </summary>
        [JsonPropertyName("flap_mode")]
        public ZabbixFlapMode FlapMode { get; set; } = ZabbixFlapMode.SuppressWindow;

        /// <summary>
        /// Suppression window, in seconds, used when <see cref="FlapMode"/> is
        /// <see cref="ZabbixFlapMode.SuppressWindow"/>. Persistence enforces a minimum of one second.
        /// </summary>
        [JsonPropertyName("flap_window_seconds")]
        public int FlapWindowSeconds { get; set; } = 120;

        /// <summary>
        /// Optional outbound caller identifier presented during alert calls.
        /// It must resolve to a non-toll-free DID owned by <see cref="ContextId"/>.
        /// Null or empty selects the platform default identifier.
        /// </summary>
        [JsonPropertyName("identifier")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public string? Identifier { get; set; }

        /// <summary>
        /// Optional DTMF digit that the callee must press to acknowledge the alert.
        /// Valid values are <see cref="MinimumDigit"/> through <see cref="MaximumDigit"/>; null disables confirmation.
        /// </summary>
        [JsonPropertyName("digit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public uint? Digit { get; set; }

        /// <summary>
        /// Optional Call Dispatch preset used to start outbound telephone calls.
        /// Null keeps the integration in validation and persistence mode without starting telephony.
        /// </summary>
        [JsonPropertyName("call_dispatch_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public Guid? CallDispatchId { get; set; }

        /// <summary>
        /// Normalized customer-owned Zabbix JSON-RPC endpoint used by guided automation.
        /// It is stored only after a successful connection test and never contains the API token.
        /// </summary>
        [JsonPropertyName("zabbix_api_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public string? ZabbixApiUrl { get; set; }

        /// <summary>
        /// Protected-at-rest Zabbix API token used for later tests and idempotent reconfiguration.
        /// This persistence-only value is excluded from all JSON serialization and must never be logged in clear form.
        /// </summary>
        [JsonIgnore]
        public string? ZabbixApiTokenProtected { get; set; }

        /// <summary>
        /// Lowest Zabbix trigger severity configured in the managed action, from 0 through 5.
        /// The default value 3 corresponds to Average severity.
        /// </summary>
        [JsonPropertyName("zabbix_minimum_severity")]
        public int ZabbixMinimumSeverity { get; set; } = 3;

        /// <summary>
        /// Identifier of the managed media type in the customer Zabbix.
        /// Null means automatic provisioning has not recorded a media type.
        /// </summary>
        [JsonPropertyName("zabbix_media_type_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public string? ZabbixMediaTypeId { get; set; }

        /// <summary>
        /// Identifier of the managed trigger action in the customer Zabbix.
        /// Null means automatic provisioning has not recorded an action.
        /// </summary>
        [JsonPropertyName("zabbix_action_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public string? ZabbixActionId { get; set; }

        /// <summary>
        /// Identifier of the Zabbix user that owns the API token used during provisioning.
        /// The managed media entry is attached to this user.
        /// </summary>
        [JsonPropertyName("zabbix_user_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public string? ZabbixUserId { get; set; }

        /// <summary>
        /// Zabbix API version returned during the latest successful connection test.
        /// </summary>
        [JsonPropertyName("zabbix_version")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public string? ZabbixVersion { get; set; }

        /// <summary>
        /// UTC instant when webhook, user media and action provisioning most recently completed.
        /// Null means the integration has not completed automatic provisioning.
        /// </summary>
        [JsonPropertyName("zabbix_last_configured_at_utc")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime? ZabbixLastConfiguredAtUtc { get; set; }

        /// <summary>
        /// Database-managed timestamp of the most recent persisted integration update.
        /// Clients should treat this value as read-only concurrency and audit metadata.
        /// </summary>
        [JsonPropertyName("timestamp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime Timestamp { get; set; }
    }
}
