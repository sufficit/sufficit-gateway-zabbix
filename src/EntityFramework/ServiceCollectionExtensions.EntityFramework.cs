using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sufficit.EFData;
using System;

namespace Sufficit.Gateway.Zabbix.EntityFramework
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSufficitGatewayZabbixEntityFramework(
            this IServiceCollection services,
            IConfiguration configuration,
            ILoggerFactory? factory = null)
        {
            services.TryAddSingleton<ZabbixGatewayEFProvider>();
            services.AddSufficitDbContextGatewayZabbix(configuration, factory);
            return services;
        }

        public static IServiceCollection AddSufficitDbContextGatewayZabbix(
            this IServiceCollection services,
            IConfiguration configuration,
            ILoggerFactory? factory = null)
        {
            var logger = factory?.CreateLogger<EFZabbixGatewayDBContext>();

            string? connectionString = configuration.GetConnectionString(Sufficit.EFData.Constants.MySql.Default);

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                string database = Sufficit.EFData.Constants.MySql.Databases.VoipRT;
                connectionString = $"{connectionString};database={database}";
                logger?.LogDebug("configuring context with full privileges on ({database})", database);
            }
            else
            {
                connectionString = configuration.GetConnectionString(Sufficit.EFData.Constants.MySql.RTRead);

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new ZabbixGatewayException(
                        ZabbixGatewayMessageCodes.DatabaseConnectionMissing,
                        ZabbixGatewayErrorKind.Configuration,
                        $"The database connection string for {nameof(EFZabbixGatewayDBContext)} was not found.");
                }

                logger?.LogDebug("configuring context with read only privileges");
            }

            services.AddDbContext<EFZabbixGatewayDBContext>(
                options => ZabbixMySqlDbContextOptions.Configure(options, connectionString!));
            return services;
        }
    }
}
