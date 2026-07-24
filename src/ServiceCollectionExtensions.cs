using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sufficit.Gateway.Zabbix.EntityFramework;
using System;

namespace Sufficit.Gateway.Zabbix
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Binds and validates the host settings required by the Zabbix gateway.
        /// This is a server-host registration used by <see cref="AddSufficitGatewayZabbix"/>.
        /// UI clients must consume the callback URL projected by the API instead of registering these options.
        /// </summary>
        public static IServiceCollection AddSufficitGatewayZabbixOptions(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var validationMessage =
                $"{ZabbixGatewayMessageCodes.PublicAlertEndpointInvalid}: " +
                $"{ZabbixGatewayOptions.SectionName}:PublicAlertEndpoint must be an absolute public HTTPS URL without query string or fragment.";
            var configuredOptions = new ZabbixGatewayOptions
            {
                PublicAlertEndpoint =
                    configuration[$"{ZabbixGatewayOptions.SectionName}:PublicAlertEndpoint"]
                    ?? string.Empty,
            };

            if (!ZabbixGatewayOptions.HasValidPublicAlertEndpoint(configuredOptions))
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.PublicAlertEndpointInvalid,
                    ZabbixGatewayErrorKind.Configuration,
                    validationMessage);
            }

            services
                .AddOptions<ZabbixGatewayOptions>()
                .Bind(configuration.GetSection(ZabbixGatewayOptions.SectionName))
                .Validate(
                    ZabbixGatewayOptions.HasValidPublicAlertEndpoint,
                    validationMessage);

            return services;
        }

        /// <summary>
        /// Registers Zabbix gateway persistence, services and validated host options.
        /// </summary>
        public static IServiceCollection AddSufficitGatewayZabbix(this IServiceCollection services, IConfiguration configuration, ILoggerFactory? factory = null)
        {
            services.AddSufficitGatewayZabbixOptions(configuration);
            services.AddSufficitGatewayZabbixEntityFramework(configuration, factory);
            services.TryAddSingleton<ZabbixGatewayService>();
            services.TryAddSingleton<ZabbixAutomationService>();
            return services;
        }
    }
}
