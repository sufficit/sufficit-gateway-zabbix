using System;

namespace Sufficit.Gateway.Zabbix
{
    /// <summary>
    /// Host-level settings used by the Zabbix gateway runtime.
    /// Bind this contract from <c>Sufficit:Gateway:Zabbix</c> in the host configuration.
    /// </summary>
    public sealed class ZabbixGatewayOptions
    {
        /// <summary>
        /// Configuration section that contains the Zabbix gateway settings.
        /// </summary>
        public const string SectionName = "Sufficit:Gateway:Zabbix";

        /// <summary>
        /// Public HTTPS endpoint called by customer Zabbix webhooks when an alert must enter the Sufficit gateway.
        /// Inform only the endpoint path, without query string or fragment; the gateway appends the tenant
        /// <c>contextId</c> and integration <c>id</c> when it provisions or displays a webhook URL.
        /// </summary>
        public string PublicAlertEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Builds the customer-specific callback URL from <see cref="PublicAlertEndpoint"/>.
        /// </summary>
        /// <param name="contextId">Tenant context that owns the Zabbix integration.</param>
        /// <param name="integrationId">Zabbix integration that will receive the alert.</param>
        /// <returns>Absolute public URL containing the required gateway query parameters.</returns>
        public string BuildAlertCallbackUrl(Guid contextId, Guid integrationId)
        {
            var builder = new UriBuilder(PublicAlertEndpoint)
            {
                Query = $"contextId={contextId:D}&id={integrationId:D}",
            };

            return builder.Uri.AbsoluteUri;
        }

        internal static bool HasValidPublicAlertEndpoint(ZabbixGatewayOptions options)
        {
            if (!Uri.TryCreate(options.PublicAlertEndpoint, UriKind.Absolute, out var uri))
                return false;

            return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(uri.DnsSafeHost)
                && string.IsNullOrEmpty(uri.UserInfo)
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment);
        }
    }
}
