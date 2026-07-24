using Microsoft.EntityFrameworkCore;
using Sufficit.Gateway.Zabbix;

namespace Sufficit.Gateway.Zabbix.EntityFramework
{
    public class EFZabbixGatewayDBContext : DbContext
    {
        public EFZabbixGatewayDBContext(DbContextOptions<EFZabbixGatewayDBContext> options) : base(options)
        {
        }

        public DbSet<ZabbixGatewayIntegration> Integrations { get; internal set; } = default!;

        public DbSet<ZabbixGatewayDestination> Destinations { get; internal set; } = default!;

        public DbSet<ZabbixAlertExecution> Executions { get; internal set; } = default!;

        public DbSet<ZabbixAlertAttempt> Attempts { get; internal set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ZabbixGatewayIntegration>(ModelBuilderHelper.Builder);
            modelBuilder.Entity<ZabbixGatewayDestination>(ModelBuilderHelper.Builder);
            modelBuilder.Entity<ZabbixAlertExecution>(ModelBuilderHelper.Builder);
            modelBuilder.Entity<ZabbixAlertAttempt>(ModelBuilderHelper.Builder);
        }
    }
}
