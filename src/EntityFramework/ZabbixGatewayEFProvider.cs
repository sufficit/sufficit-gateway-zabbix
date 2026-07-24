using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sufficit.Gateway.Zabbix;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Sufficit.Gateway.Zabbix.EntityFramework
{
    /// <summary>
    /// Entity Framework provider for Zabbix gateway persistence.
    /// It owns integration/destination CRUD and the durable execution/attempt records used by the alert workflow.
    /// </summary>
    /// <remarks>
    /// This provider is the database boundary only. It translates persistence requests into Entity
    /// Framework operations and must not authorize HTTP users, publish business events, create audit
    /// policy, or own application metrics. Those responsibilities belong to the caller and to the
    /// standard Zabbix runtime respectively.
    /// </remarks>
    public class ZabbixGatewayEFProvider : ScopedProvider
    {
        private const int MinFlapWindowSeconds = 1;
        private const int MinPriority = 1;

        /// <summary>
        /// Creates the Zabbix Entity Framework persistence provider.
        /// </summary>
        /// <param name="serviceScopeFactory">Factory used to create an isolated database scope per operation.</param>
        public ZabbixGatewayEFProvider(IServiceScopeFactory serviceScopeFactory)
            : base(serviceScopeFactory)
        {
        }

        private static async Task AddOrUpdate<T>(
            DbContext dbContext,
            T model,
            CancellationToken cancellationToken)
            where T : class
        {
            var entityType = dbContext.Model.FindEntityType(typeof(T));
            var primaryKey = entityType?.FindPrimaryKey()
                ?? throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.EntityPrimaryKeyMissing,
                    ZabbixGatewayErrorKind.Internal,
                    $"Cannot persist {typeof(T).Name} because its primary-key metadata is missing.");

            var keys = primaryKey.Properties
                .Select(property => property.PropertyInfo?.GetValue(model) ?? property.FieldInfo?.GetValue(model))
                .ToArray();
            var current = await dbContext.FindAsync<T>(keys, cancellationToken);

            if (current is null)
            {
                await dbContext.AddAsync(model, cancellationToken);
                return;
            }

            dbContext.Entry(current).State = EntityState.Detached;
            dbContext.Update(model);
        }

        private static EFZabbixGatewayDBContext CreateDbContext(IServiceScope scope)
            => scope.ServiceProvider.GetRequiredService<EFZabbixGatewayDBContext>();

        /// <summary>
        /// Streams integrations that match the informed search parameters.
        /// Used by the API/UI configuration flow and by helper methods such as <c>GetByIdAsync</c>.
        /// </summary>
        public virtual async IAsyncEnumerable<ZabbixGatewayIntegration> Search(
            ZabbixGatewaySearchParameters parameters,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var scope = CreateAsyncScope();
            using var dbContext = CreateDbContext(scope);

            var query = ParseSearchParameters(dbContext.Integrations.AsQueryable(), parameters);
            await foreach (var item in query.AsNoTracking().AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }

        /// <summary>
        /// Applies the supported integration filters to an EF query.
        /// This centralizes the provider search contract so API and internal lookups stay aligned.
        /// </summary>
        public static IQueryable<ZabbixGatewayIntegration> ParseSearchParameters(
            IQueryable<ZabbixGatewayIntegration> query,
            ZabbixGatewaySearchParameters parameters)
        {
            if (parameters.Id.HasValue)
                query = query.Where(s => s.Id == parameters.Id.Value);

            if (parameters.ContextId.HasValue)
                query = query.Where(s => s.ContextId == parameters.ContextId.Value);

            if (parameters.Enabled.HasValue)
                query = query.Where(s => s.Enabled == parameters.Enabled.Value);

            if (parameters.Title?.IsValid == true)
            {
                var title = parameters.Title.Text.ToLower();
                query = parameters.Title.ExactMatch
                    ? query.Where(s => s.Title != null && s.Title.ToLower() == title)
                    : query.Where(s => s.Title != null && s.Title.ToLower().Contains(title));
            }

            if (parameters.Limit.HasValue && parameters.Limit.Value > 0)
                query = query.OrderBy(a => a.Id).Take((int)parameters.Limit.Value);

            return query;
        }

        /// <summary>
        /// Inserts or updates a Zabbix integration configuration row.
        /// It guarantees the integration identifier and enforces the minimum flap window before persisting to <c>gatw_zabbix_integrations</c>.
        /// </summary>
        public async Task<ZabbixGatewayIntegration> Update(ZabbixGatewayIntegration entity, CancellationToken cancellationToken = default)
        {
            if (entity.Id == Guid.Empty)
                entity.Id = Guid.NewGuid();

            if (entity.FlapWindowSeconds < MinFlapWindowSeconds)
                entity.FlapWindowSeconds = MinFlapWindowSeconds;

            using var scope = CreateAsyncScope();
            using var dbContext = CreateDbContext(scope);

            var current = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                dbContext.Integrations,
                item => item.Id == entity.Id,
                cancellationToken);

            if (current is null)
            {
                dbContext.Integrations.Add(entity);
                current = entity;
            }
            else
            {
                // Keep automation credentials and remote object identifiers isolated from
                // the legacy preferences endpoint, including during concurrent saves.
                current.ContextId = entity.ContextId;
                current.Title = entity.Title;
                current.Enabled = entity.Enabled;
                current.FlapMode = entity.FlapMode;
                current.FlapWindowSeconds = entity.FlapWindowSeconds;
                current.Identifier = entity.Identifier;
                current.Digit = entity.Digit;
                current.CallDispatchId = entity.CallDispatchId;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return current;
        }

        /// <summary>
        /// Updates only the customer Zabbix automation fields, preserving the alert and telephony preferences.
        /// </summary>
        public virtual async Task<ZabbixGatewayIntegration> UpdateAutomation(
            Guid id,
            string apiUrl,
            string protectedToken,
            int minimumSeverity,
            string? version,
            string? userId,
            string? mediaTypeId,
            string? actionId,
            DateTime? lastConfiguredAtUtc,
            CancellationToken cancellationToken = default)
        {
            using var scope = CreateAsyncScope();
            using var dbContext = CreateDbContext(scope);

            var entity = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                dbContext.Integrations,
                item => item.Id == id,
                cancellationToken)
                ?? throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.IntegrationNotFound,
                    ZabbixGatewayErrorKind.NotFound,
                    $"Zabbix integration not found: {id}.");

            entity.ZabbixApiUrl = apiUrl;
            entity.ZabbixApiTokenProtected = protectedToken;
            entity.ZabbixMinimumSeverity = Math.Min(5, Math.Max(0, minimumSeverity));
            entity.ZabbixVersion = version;
            entity.ZabbixUserId = userId;
            entity.ZabbixMediaTypeId = mediaTypeId;
            entity.ZabbixActionId = actionId;
            entity.ZabbixLastConfiguredAtUtc = lastConfiguredAtUtc;

            await dbContext.SaveChangesAsync(cancellationToken);
            return entity;
        }

        /// <summary>
        /// Inserts or updates a destination row for an integration.
        /// It normalizes the minimum priority so escalation order remains valid in <c>gatw_zabbix_destinations</c>.
        /// </summary>
        public async Task<ZabbixGatewayDestination> UpdateDestination(ZabbixGatewayDestination entity, CancellationToken cancellationToken = default)
        {
            if (entity.Id == Guid.Empty)
                entity.Id = Guid.NewGuid();

            if (entity.Priority < MinPriority)
                entity.Priority = MinPriority;

            using var scope = CreateAsyncScope();
            using var dbContext = CreateDbContext(scope);

            await AddOrUpdate(dbContext, entity, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return entity;
        }

        /// <summary>
        /// Returns the configured destinations for one integration in execution order.
        /// The standard start workflow uses this to count enabled targets before creating an execution.
        /// </summary>
        public async Task<IReadOnlyList<ZabbixGatewayDestination>> ListDestinations(
            Guid integrationId,
            CancellationToken cancellationToken = default)
        {
            using var scope = CreateAsyncScope();
            using var dbContext = CreateDbContext(scope);

            return await dbContext.Destinations
                .AsQueryable()
                .AsNoTracking()
                .Where(s => s.IntegrationId == integrationId)
                .OrderBy(s => s.Priority)
                .ThenBy(s => s.Id)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Returns one destination by identifier, optionally constrained to an already-authorized tenant context.
        /// The context constraint lets application callers resolve a destination without crossing tenant boundaries.
        /// </summary>
        public async Task<ZabbixGatewayDestination?> GetDestinationById(
            Guid id,
            Guid? contextId = null,
            CancellationToken cancellationToken = default)
        {
            using var scope = CreateAsyncScope();
            using var dbContext = CreateDbContext(scope);

            var query = dbContext.Destinations
                .AsQueryable()
                .AsNoTracking()
                .Where(item => item.Id == id);

            if (contextId.HasValue)
                query = query.Where(item => item.ContextId == contextId.Value);

            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        /// <summary>
        /// Streams persisted alert executions that match the informed monitoring filters.
        /// Used by endpoints and Blazor to show recent history for one integration.
        /// </summary>
        public virtual async IAsyncEnumerable<ZabbixAlertExecution> SearchExecutions(
            ZabbixAlertExecutionSearchParameters parameters,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var scope = CreateAsyncScope();
            using var dbContext = CreateDbContext(scope);

            var query = ParseExecutionSearchParameters(dbContext.Executions.AsQueryable(), parameters);
            await foreach (var item in query.AsNoTracking().AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }

        /// <summary>
        /// Applies execution-monitoring filters and ordering to an EF query.
        /// The result is always sorted by newest start time first so dashboards see the latest events first.
        /// </summary>
        public static IQueryable<ZabbixAlertExecution> ParseExecutionSearchParameters(
            IQueryable<ZabbixAlertExecution> query,
            ZabbixAlertExecutionSearchParameters parameters)
        {
            if (parameters.AlertId.HasValue)
                query = query.Where(s => s.Id == parameters.AlertId.Value);

            if (parameters.ContextId.HasValue)
                query = query.Where(s => s.ContextId == parameters.ContextId.Value);

            if (parameters.IntegrationId.HasValue)
                query = query.Where(s => s.IntegrationId == parameters.IntegrationId.Value);

            if (parameters.Status.HasValue)
                query = query.Where(s => s.Status == parameters.Status.Value);

            query = query.OrderByDescending(s => s.StartedAtUtc).ThenByDescending(s => s.Id);

            if (parameters.Limit.HasValue && parameters.Limit.Value > 0)
                query = query.Take((int)parameters.Limit.Value);

            return query;
        }

            /// <summary>
            /// Inserts or updates one persisted alert execution row.
            /// Called by the standard Zabbix provider to write the durable start state into <c>gatw_zabbix_executions</c>.
            /// </summary>
        public async Task<ZabbixAlertExecution> UpdateExecution(ZabbixAlertExecution entity, CancellationToken cancellationToken = default)
        {
            if (entity.Id == Guid.Empty)
                entity.Id = Guid.NewGuid();

            using var scope = CreateAsyncScope();
            using var dbContext = CreateDbContext(scope);

            await AddOrUpdate(dbContext, entity, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return entity;
        }

        /// <summary>
        /// Inserts or updates one persisted dialing attempt row.
        /// Prepared for the upcoming outbound dispatcher that will track retries/escalation in <c>gatw_zabbix_attempts</c>.
        /// </summary>
        public async Task<ZabbixAlertAttempt> UpdateAttempt(ZabbixAlertAttempt entity, CancellationToken cancellationToken = default)
        {
            if (entity.Id == Guid.Empty)
                entity.Id = Guid.NewGuid();

            using var scope = CreateAsyncScope();
            using var dbContext = CreateDbContext(scope);

            await AddOrUpdate(dbContext, entity, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return entity;
        }

        /// <summary>
        /// Loads one execution by its public alert identifier.
        /// Intended for future status endpoints and for operational troubleshooting of the start workflow.
        /// </summary>
        public async Task<ZabbixAlertExecution?> GetExecutionById(Guid alertId, CancellationToken cancellationToken = default)
        {
            using var scope = CreateAsyncScope();
            using var dbContext = CreateDbContext(scope);

            return await dbContext.Executions
                .AsQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == alertId, cancellationToken);
        }

            /// <summary>
            /// Lists all attempts already persisted for a given alert execution.
            /// Results are ordered by attempt number and start time so callers can reconstruct escalation history.
            /// </summary>
        public async Task<IReadOnlyList<ZabbixAlertAttempt>> ListAttempts(Guid alertId, CancellationToken cancellationToken = default)
        {
            using var scope = CreateAsyncScope();
            using var dbContext = CreateDbContext(scope);

            return await dbContext.Attempts
                .AsQueryable()
                .AsNoTracking()
                .Where(s => s.AlertId == alertId)
                .OrderBy(s => s.AttemptNumber)
                .ThenBy(s => s.StartedAtUtc)
                .ToListAsync(cancellationToken);
        }

            /// <summary>
            /// Removes one integration and its configured destinations.
            /// This is the configuration-side delete path and does not target historical execution or attempt rows.
            /// </summary>
        public async Task<bool> Delete(Guid id, CancellationToken cancellationToken = default)
        {
            using var scope = CreateAsyncScope();
            using var dbContext = CreateDbContext(scope);

            var integration = await dbContext.Integrations
                .AsQueryable()
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (integration == null)
                return false;

            var destinations = await dbContext.Destinations
                .AsQueryable()
                .Where(s => s.IntegrationId == id)
                .ToListAsync(cancellationToken);
            dbContext.Destinations.RemoveRange(destinations);
            dbContext.Integrations.Remove(integration);

            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>
        /// Removes a single configured destination.
        /// Used by the configuration API when editing an integration destination list.
        /// </summary>
        public async Task<bool> DeleteDestination(Guid id, CancellationToken cancellationToken = default)
        {
            using var scope = CreateAsyncScope();
            using var dbContext = CreateDbContext(scope);

            var destination = await dbContext.Destinations
                .AsQueryable()
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (destination == null)
                return false;

            dbContext.Destinations.Remove(destination);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

    }
}
