namespace Sufficit.Gateway.Zabbix
{
    /// <summary>
    /// Defines how an integration reacts when Zabbix repeatedly sends the same effective problem.
    /// </summary>
    public enum ZabbixFlapMode
    {
        /// <summary>
        /// Accept every matching event and allow each occurrence to start a new alert execution.
        /// Use this only when every repetition must produce another telephone call.
        /// </summary>
        AlwaysCall = 0,

        /// <summary>
        /// Suppress repeated occurrences with the same flap fingerprint during
        /// <see cref="ZabbixGatewayIntegration.FlapWindowSeconds"/>.
        /// </summary>
        SuppressWindow = 1,
    }
}
