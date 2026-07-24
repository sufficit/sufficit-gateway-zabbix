using Sufficit.Gateway.Zabbix;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sufficit.Gateway.Zabbix.EntityFramework
{
    /// <summary>
    /// Convenience helpers for <see cref="ZabbixGatewayEFProvider"/> queries.
    /// They keep common single-item lookups out of the provider core while still reusing the canonical search pipeline.
    /// </summary>
    public static class ZabbixGatewayEFProviderExtensions
    {
        /// <summary>
        /// Returns a single integration by identifier using the provider search contract.
        /// Used by the standard Zabbix start workflow to resolve the configuration before validation.
        /// </summary>
        public static async Task<ZabbixGatewayIntegration?> GetByIdAsync(
            this ZabbixGatewayEFProvider source,
            Guid id,
            CancellationToken cancellationToken = default)
        {
            await foreach (var item in source.Search(new ZabbixGatewaySearchParameters { Id = id, Limit = 1 }, cancellationToken))
            {
                return item;
            }

            return null;
        }
    }
}
