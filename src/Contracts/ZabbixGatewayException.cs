using System;

namespace Sufficit.Gateway.Zabbix
{
    /// <summary>
    /// Exception carrying a stable Zabbix gateway message code and a transport-neutral
    /// error category. Its message is always an English technical fallback.
    /// </summary>
    public sealed class ZabbixGatewayException : Exception
    {
        /// <summary>
        /// Creates a coded gateway exception.
        /// </summary>
        /// <param name="code">Stable code from <see cref="ZabbixGatewayMessageCodes"/>.</param>
        /// <param name="kind">Transport-neutral error category.</param>
        /// <param name="message">English technical fallback message.</param>
        /// <param name="innerException">Optional lower-level cause retained for diagnostics.</param>
        public ZabbixGatewayException(
            string code,
            ZabbixGatewayErrorKind kind,
            string message,
            Exception? innerException = null)
            : base(message, innerException)
        {
            Code = string.IsNullOrWhiteSpace(code)
                ? ZabbixGatewayMessageCodes.UnexpectedError
                : code;
            Kind = kind;
        }

        /// <summary>
        /// Stable localization key unique to the Sufficit Zabbix gateway project.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Transport-neutral category that API hosts can map to an HTTP or RPC status.
        /// </summary>
        public ZabbixGatewayErrorKind Kind { get; }
    }
}
