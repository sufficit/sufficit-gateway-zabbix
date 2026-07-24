using System.Text.Json.Serialization;

namespace Sufficit.Gateway.Zabbix
{
    /// <summary>
    /// Transport-neutral category used by API hosts to map a coded gateway error
    /// to an appropriate protocol status.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ZabbixGatewayErrorKind
    {
        /// <summary>The request contains a missing, malformed or unsafe value.</summary>
        Validation,

        /// <summary>The requested Zabbix gateway resource does not exist.</summary>
        NotFound,

        /// <summary>The authenticated caller cannot access the requested resource.</summary>
        Forbidden,

        /// <summary>The current persisted or remote state prevents the operation.</summary>
        Conflict,

        /// <summary>A customer-owned Zabbix or telephony dependency failed.</summary>
        ExternalService,

        /// <summary>The host application is missing required gateway configuration.</summary>
        Configuration,

        /// <summary>An unexpected internal gateway failure occurred.</summary>
        Internal,
    }
}
