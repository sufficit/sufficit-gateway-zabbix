using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace Sufficit.Gateway.Zabbix.EntityFramework
{
    internal static class ZabbixMySqlDbContextOptions
    {
        private const int CommandTimeoutSeconds = 30;

        public static void Configure(DbContextOptionsBuilder options, string connectionString)
        {
#if NET5_0_OR_GREATER
            var version = ServerVersions.GetOrAdd(connectionString, value => ServerVersion.AutoDetect(value));
            options.UseMySql(
                connectionString,
                version,
                provider => provider
                    .EnableRetryOnFailure(2)
                    .CommandTimeout(CommandTimeoutSeconds));
#else
            options.UseMySql(
                connectionString,
                provider => provider
                    .EnableRetryOnFailure(2)
                    .CommandTimeout(CommandTimeoutSeconds));
#endif
        }

#if NET5_0_OR_GREATER
        private static readonly ConcurrentDictionary<string, ServerVersion> ServerVersions =
            new ConcurrentDictionary<string, ServerVersion>();
#endif
    }
}
