using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sufficit.Gateway.Zabbix;
using System;

namespace Sufficit.Gateway.Zabbix.EntityFramework
{
    public static class ModelBuilderHelper
    {
        public const string TABLEPREFIX_GT_ZB = "gatw_zabbix_";

        public static void Builder(EntityTypeBuilder<ZabbixGatewayIntegration> entity)
        {
            entity.ToTable($"{TABLEPREFIX_GT_ZB}integrations");

            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.ContextId, a.Title }).IsUnique();

            entity.Property(a => a.Id)
                .HasColumnName("id")
                .HasColumnType("BINARY(16)")
                .HasConversion(v => v.ToByteArray(), v => new Guid(v));

            entity.Property(a => a.ContextId)
                .HasColumnName("contextid")
                .HasColumnType("BINARY(16)")
                .HasConversion(v => v.ToByteArray(), v => new Guid(v));

            entity.Property(a => a.Title)
                .HasColumnName("title")
                .HasColumnType("VARCHAR(255)");

            entity.Property(a => a.Enabled)
                .HasColumnName("enabled")
                .HasColumnType("BIT(1)");

            entity.Property(a => a.FlapMode)
                .HasColumnName("flap_mode")
                .HasColumnType("INT(1)")
                .HasConversion<int>();

            entity.Property(a => a.FlapWindowSeconds)
                .HasColumnName("flap_window_seconds")
                .HasColumnType("INT(11)");

            entity.Property(a => a.Identifier)
                .HasColumnName("identifier")
                .HasColumnType("VARCHAR(40)");

            entity.Property(a => a.Digit)
                .HasColumnName("digit")
                .HasColumnType("INT(1) UNSIGNED");

            entity.Property(a => a.CallDispatchId)
                .HasColumnName("call_dispatch_id")
                .HasColumnType("BINARY(16)")
                .HasConversion(v => v.HasValue ? v.Value.ToByteArray() : null, v => v == null ? (Guid?)null : new Guid(v));

            entity.Property(a => a.ZabbixApiUrl)
                .HasColumnName("zabbix_api_url")
                .HasColumnType("VARCHAR(2048)");

            entity.Property(a => a.ZabbixApiTokenProtected)
                .HasColumnName("zabbix_api_token_protected")
                .HasColumnType("TEXT");

            entity.Property(a => a.ZabbixMinimumSeverity)
                .HasColumnName("zabbix_minimum_severity")
                .HasColumnType("INT(1)");

            entity.Property(a => a.ZabbixMediaTypeId)
                .HasColumnName("zabbix_media_type_id")
                .HasColumnType("VARCHAR(32)");

            entity.Property(a => a.ZabbixActionId)
                .HasColumnName("zabbix_action_id")
                .HasColumnType("VARCHAR(32)");

            entity.Property(a => a.ZabbixUserId)
                .HasColumnName("zabbix_user_id")
                .HasColumnType("VARCHAR(32)");

            entity.Property(a => a.ZabbixVersion)
                .HasColumnName("zabbix_version")
                .HasColumnType("VARCHAR(32)");

            entity.Property(a => a.ZabbixLastConfiguredAtUtc)
                .HasColumnName("zabbix_last_configured_at_utc")
                .HasColumnType("DATETIME(6)")
                .HasSqlDateTimeAdjust();

            entity.Property(a => a.Timestamp)
                .HasColumnName("update")
                .HasColumnType("TIMESTAMP")
                .HasSqlDateTimeAdjust(true, true);
        }

        public static void Builder(EntityTypeBuilder<ZabbixGatewayDestination> entity)
        {
            entity.ToTable($"{TABLEPREFIX_GT_ZB}destinations");

            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.IntegrationId, a.Priority, a.Id });

            entity.Property(a => a.Id)
                .HasColumnName("id")
                .HasColumnType("BINARY(16)")
                .HasConversion(v => v.ToByteArray(), v => new Guid(v));

            entity.Property(a => a.IntegrationId)
                .HasColumnName("integrationid")
                .HasColumnType("BINARY(16)")
                .HasConversion(v => v.ToByteArray(), v => new Guid(v));

            entity.Property(a => a.ContextId)
                .HasColumnName("contextid")
                .HasColumnType("BINARY(16)")
                .HasConversion(v => v.ToByteArray(), v => new Guid(v));

            entity.Property(a => a.Title)
                .HasColumnName("title")
                .HasColumnType("VARCHAR(255)");

            entity.Property(a => a.PhoneNumber)
                .HasColumnName("phone_number")
                .HasColumnType("VARCHAR(40)");

            entity.Property(a => a.Priority)
                .HasColumnName("priority")
                .HasColumnType("INT(11)");

            entity.Property(a => a.Enabled)
                .HasColumnName("enabled")
                .HasColumnType("BIT(1)");

            entity.Property(a => a.Timestamp)
                .HasColumnName("update")
                .HasColumnType("TIMESTAMP")
                .HasSqlDateTimeAdjust(true, true);

            entity.HasOne<ZabbixGatewayIntegration>()
                .WithMany()
                .HasForeignKey(a => a.IntegrationId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        public static void Builder(EntityTypeBuilder<ZabbixAlertExecution> entity)
        {
            entity.ToTable($"{TABLEPREFIX_GT_ZB}executions");

            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.ContextId, a.IntegrationId, a.SourceEventId }).IsUnique();
            entity.HasIndex(a => new { a.ContextId, a.IntegrationId, a.Status, a.StartedAtUtc });

            entity.Property(a => a.Id)
                .HasColumnName("id")
                .HasColumnType("BINARY(16)")
                .HasConversion(v => v.ToByteArray(), v => new Guid(v));

            entity.Property(a => a.ContextId)
                .HasColumnName("contextid")
                .HasColumnType("BINARY(16)")
                .HasConversion(v => v.ToByteArray(), v => new Guid(v));

            entity.Property(a => a.IntegrationId)
                .HasColumnName("integrationid")
                .HasColumnType("BINARY(16)")
                .HasConversion(v => v.ToByteArray(), v => new Guid(v));

            entity.Property(a => a.SourceEventId)
                .HasColumnName("source_event_id")
                .HasColumnType("VARCHAR(255)");

            entity.Property(a => a.Host)
                .HasColumnName("host")
                .HasColumnType("VARCHAR(255)");

            entity.Property(a => a.Trigger)
                .HasColumnName("trigger")
                .HasColumnType("VARCHAR(500)");

            entity.Property(a => a.Severity)
                .HasColumnName("severity")
                .HasColumnType("VARCHAR(50)");

            entity.Property(a => a.Subject)
                .HasColumnName("subject")
                .HasColumnType("VARCHAR(500)");

            entity.Property(a => a.Identifier)
                .HasColumnName("identifier")
                .HasColumnType("VARCHAR(40)");

            entity.Property(a => a.Digit)
                .HasColumnName("digit")
                .HasColumnType("INT(1) UNSIGNED");

            entity.Property(a => a.CallDispatchId)
                .HasColumnName("call_dispatch_id")
                .HasColumnType("BINARY(16)")
                .HasConversion(v => v.HasValue ? v.Value.ToByteArray() : null, v => v == null ? (Guid?)null : new Guid(v));

            entity.Property(a => a.UsesDefaultIdentifier)
                .HasColumnName("uses_default_identifier")
                .HasColumnType("BIT(1)");

            entity.Property(a => a.FlapKey)
                .HasColumnName("flap_key")
                .HasColumnType("VARCHAR(500)");

            entity.Property(a => a.Status)
                .HasColumnName("status")
                .HasColumnType("VARCHAR(40)")
                .HasConversion<string>();

            entity.Property(a => a.StartedAtUtc)
                .HasColumnName("started_at_utc")
                .HasColumnType("DATETIME(6)")
                .HasSqlDateTimeAdjust(false, false);

            entity.Property(a => a.FinishedAtUtc)
                .HasColumnName("finished_at_utc")
                .HasColumnType("DATETIME(6)")
                .HasSqlDateTimeAdjust();

            entity.Property(a => a.ErrorCode)
                .HasColumnName("error_code")
                .HasColumnType("VARCHAR(16)");

            entity.Property(a => a.Error)
                .HasColumnName("error")
                .HasColumnType("VARCHAR(500)");

            entity.Property(a => a.Timestamp)
                .HasColumnName("update")
                .HasColumnType("TIMESTAMP")
                .HasSqlDateTimeAdjust(true, true);

            entity.HasOne<ZabbixGatewayIntegration>()
                .WithMany()
                .HasForeignKey(a => a.IntegrationId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        public static void Builder(EntityTypeBuilder<ZabbixAlertAttempt> entity)
        {
            entity.ToTable($"{TABLEPREFIX_GT_ZB}attempts");

            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.AlertId, a.AttemptNumber, a.StartedAtUtc });

            entity.Property(a => a.Id)
                .HasColumnName("id")
                .HasColumnType("BINARY(16)")
                .HasConversion(v => v.ToByteArray(), v => new Guid(v));

            entity.Property(a => a.AlertId)
                .HasColumnName("alertid")
                .HasColumnType("BINARY(16)")
                .HasConversion(v => v.ToByteArray(), v => new Guid(v));

            entity.Property(a => a.ContextId)
                .HasColumnName("contextid")
                .HasColumnType("BINARY(16)")
                .HasConversion(v => v.ToByteArray(), v => new Guid(v));

            entity.Property(a => a.DestinationId)
                .HasColumnName("destinationid")
                .HasColumnType("BINARY(16)")
                .HasConversion(v => v.ToByteArray(), v => new Guid(v));

            entity.Property(a => a.DestinationTitle)
                .HasColumnName("destination_title")
                .HasColumnType("VARCHAR(255)");

            entity.Property(a => a.DispatchId)
                .HasColumnName("dispatch_id")
                .HasColumnType("BINARY(16)")
                .HasConversion(v => v.HasValue ? v.Value.ToByteArray() : null, v => v == null ? (Guid?)null : new Guid(v));

            entity.Property(a => a.PhoneNumber)
                .HasColumnName("phone_number")
                .HasColumnType("VARCHAR(40)");

            entity.Property(a => a.Priority)
                .HasColumnName("priority")
                .HasColumnType("INT(11)");

            entity.Property(a => a.AttemptNumber)
                .HasColumnName("attempt_number")
                .HasColumnType("INT(11)");

            entity.Property(a => a.Status)
                .HasColumnName("status")
                .HasColumnType("VARCHAR(40)")
                .HasConversion<string>();

            entity.Property(a => a.StartedAtUtc)
                .HasColumnName("started_at_utc")
                .HasColumnType("DATETIME(6)")
                .HasSqlDateTimeAdjust(false, false);

            entity.Property(a => a.FinishedAtUtc)
                .HasColumnName("finished_at_utc")
                .HasColumnType("DATETIME(6)")
                .HasSqlDateTimeAdjust();

            entity.Property(a => a.ErrorCode)
                .HasColumnName("error_code")
                .HasColumnType("VARCHAR(16)");

            entity.Property(a => a.Error)
                .HasColumnName("error")
                .HasColumnType("VARCHAR(500)");

            entity.Property(a => a.Timestamp)
                .HasColumnName("update")
                .HasColumnType("TIMESTAMP")
                .HasSqlDateTimeAdjust(true, true);

            entity.HasOne<ZabbixAlertExecution>()
                .WithMany()
                .HasForeignKey(a => a.AlertId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
