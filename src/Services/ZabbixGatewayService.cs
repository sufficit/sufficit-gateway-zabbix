using Microsoft.Extensions.Logging;
using Sufficit.Gateway.Zabbix.EntityFramework;
using Sufficit.Telephony;
using Sufficit.Telephony.CallDispatch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sufficit.Gateway.Zabbix
{
    /// <summary>
    /// Central operational service for Zabbix telephony alert starts.
    /// It validates integration ownership, validates the effective caller identifier and persists the initial execution snapshot.
    /// </summary>
    public class ZabbixGatewayService
    {
        private const string FlapKeySeparator = "|";
        private const int MaxDispatchLabelLength = 120;

        private readonly ZabbixGatewayEFProvider _provider;
        private readonly IZabbixTelephonyBridge _telephony;
        private readonly ILogger<ZabbixGatewayService> _logger;

        public ZabbixGatewayService(
            ZabbixGatewayEFProvider provider,
            IZabbixTelephonyBridge telephony,
            ILogger<ZabbixGatewayService> logger)
        {
            _provider = provider;
            _telephony = telephony;
            _logger = logger;
        }

        /// <summary>
        /// Validates the alert start request and persists a <see cref="ZabbixAlertExecution"/> row in validated state.
        /// Called by the endpoints layer as the synchronous entry point of the Zabbix alert workflow.
        /// </summary>
        public async Task<ZabbixAlertStartResult> StartAlertAsync(Guid contextId, Guid id, ZabbixAlertStartRequest? request, CancellationToken cancellationToken = default)
        {
            if (contextId == Guid.Empty)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.ContextIdRequired,
                    ZabbixGatewayErrorKind.Validation,
                    "A tenant context identifier is required.");
            }

            if (id == Guid.Empty)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.IntegrationIdRequired,
                    ZabbixGatewayErrorKind.Validation,
                    "A Zabbix integration identifier is required.");
            }

            var integration = await GetActiveIntegrationAsync(contextId, id, cancellationToken);
            ValidateDigit(integration.Digit);
            var resolvedIdentifier = await ValidateIdentifierAsync(contextId, integration.Identifier, cancellationToken);
            var destinations = await _provider.ListDestinations(id, cancellationToken);
            var enabledDestinations = destinations.Where(a => a.Enabled).OrderBy(a => a.Priority).ThenBy(a => a.Id).ToList();

            var alertId = Guid.NewGuid();
            var startedAtUtc = DateTime.UtcNow;
            var usesDefaultIdentifier = string.IsNullOrWhiteSpace(integration.Identifier);
            var identifier = resolvedIdentifier?.Extension ?? integration.Identifier;
            var execution = new ZabbixAlertExecution
            {
                Id = alertId,
                ContextId = contextId,
                IntegrationId = integration.Id,
                SourceEventId = NormalizeOptionalText(request?.SourceEventId),
                Host = NormalizeOptionalText(request?.Host),
                Trigger = NormalizeOptionalText(request?.Trigger),
                Severity = NormalizeOptionalText(request?.Severity),
                Subject = NormalizeOptionalText(request?.Subject),
                Identifier = NormalizeOptionalText(identifier),
                Digit = integration.Digit,
                CallDispatchId = integration.CallDispatchId,
                UsesDefaultIdentifier = usesDefaultIdentifier,
                FlapKey = BuildFlapKey(request),
                Status = ZabbixAlertExecutionStatus.Validated,
                StartedAtUtc = startedAtUtc,
            };

            await _provider.UpdateExecution(execution, cancellationToken);

            string message;
            if (enabledDestinations.Count == 0)
            {
                message = "alert start validated without enabled destinations";

                _logger.LogInformation(
                    "zabbix alert start validated without enabled destinations: alert={alertid}, integration={id}, context={contextid}, identifier={identifier}, defaultIdentifier={defaultidentifier}, sourceEventId={sourceeventid}",
                    alertId,
                    integration.Id,
                    contextId,
                    identifier,
                    usesDefaultIdentifier,
                    request?.SourceEventId);

                return new ZabbixAlertStartResult
                {
                    Accepted = true,
                    AlertId = execution.Id,
                    ContextId = execution.ContextId,
                    Id = execution.IntegrationId,
                    Identifier = execution.Identifier,
                    Digit = execution.Digit,
                    UsesDefaultIdentifier = execution.UsesDefaultIdentifier,
                    ValidatedDestinationCount = enabledDestinations.Count,
                    Status = execution.Status,
                    MessageCode = ZabbixGatewayMessageCodes.AlertAcceptedWithoutDestinations,
                    Message = message,
                };
            }

            if (!integration.CallDispatchId.HasValue || integration.CallDispatchId.Value == Guid.Empty)
            {
                message = "alert start validated without call dispatch configuration";

                _logger.LogWarning(
                    "zabbix alert start validated without call dispatch configuration: alert={alertid}, integration={id}, context={contextid}, destinations={destinations}",
                    alertId,
                    integration.Id,
                    contextId,
                    enabledDestinations.Count);

                return new ZabbixAlertStartResult
                {
                    Accepted = true,
                    AlertId = execution.Id,
                    ContextId = execution.ContextId,
                    Id = execution.IntegrationId,
                    Identifier = execution.Identifier,
                    Digit = execution.Digit,
                    UsesDefaultIdentifier = execution.UsesDefaultIdentifier,
                    ValidatedDestinationCount = enabledDestinations.Count,
                    Status = execution.Status,
                    MessageCode = ZabbixGatewayMessageCodes.AlertAcceptedWithoutCallDispatch,
                    Message = message,
                };
            }

            CallDispatchConfiguration callDispatchConfiguration;
            try
            {
                callDispatchConfiguration = await GetCallDispatchConfigurationAsync(contextId, integration.CallDispatchId.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                execution.Status = ZabbixAlertExecutionStatus.Failed;
                execution.FinishedAtUtc = DateTime.UtcNow;
                execution.ErrorCode = ex is ZabbixGatewayException gatewayException
                    ? gatewayException.Code
                    : ZabbixGatewayMessageCodes.CallDispatchValidationFailed;
                execution.Error = "Call Dispatch configuration validation failed.";
                await _provider.UpdateExecution(execution, cancellationToken);

                message = "call dispatch configuration validation failed";

                _logger.LogError(
                    ex,
                    "zabbix alert start failed while validating call dispatch configuration: alert={alertid}, integration={id}, context={contextid}, callDispatchId={calldispatchid}",
                    alertId,
                    integration.Id,
                    contextId,
                    integration.CallDispatchId);

                return new ZabbixAlertStartResult
                {
                    Accepted = true,
                    AlertId = execution.Id,
                    ContextId = execution.ContextId,
                    Id = execution.IntegrationId,
                    Identifier = execution.Identifier,
                    Digit = execution.Digit,
                    UsesDefaultIdentifier = execution.UsesDefaultIdentifier,
                    ValidatedDestinationCount = enabledDestinations.Count,
                    Status = execution.Status,
                    MessageCode = ZabbixGatewayMessageCodes.CallDispatchValidationFailed,
                    Message = message,
                };
            }

            var startedDispatches = 0;
            var failedDispatches = 0;
            var attemptNumber = 0;

            foreach (var destination in enabledDestinations)
            {
                attemptNumber++;

                var attempt = new ZabbixAlertAttempt
                {
                    Id = Guid.NewGuid(),
                    AlertId = execution.Id,
                    ContextId = execution.ContextId,
                    DestinationId = destination.Id,
                    DestinationTitle = NormalizeOptionalText(destination.Title),
                    PhoneNumber = destination.PhoneNumber,
                    Priority = destination.Priority,
                    AttemptNumber = attemptNumber,
                    Status = ZabbixAlertAttemptStatus.Pending,
                    StartedAtUtc = DateTime.UtcNow,
                };

                try
                {
                    var dispatchResult = await _telephony.StartCallDispatchAsync(
                        BuildCallDispatchRequest(contextId, execution.Id, integration, callDispatchConfiguration, destination, request),
                        cancellationToken);

                    attempt.DispatchId = dispatchResult.DispatchId == Guid.Empty ? null : dispatchResult.DispatchId;
                    attempt.Status = ZabbixAlertAttemptStatus.Running;
                    attempt.Error = null;

                    startedDispatches++;

                    _logger.LogInformation(
                        "zabbix alert dispatch started: alert={alertid}, integration={id}, attempt={attemptnumber}, destination={destinationid}, dispatch={dispatchid}, callDispatchId={calldispatchid}",
                        execution.Id,
                        integration.Id,
                        attempt.AttemptNumber,
                        destination.Id,
                        attempt.DispatchId,
                        callDispatchConfiguration.Id);
                }
                catch (Exception ex)
                {
                    attempt.Status = ZabbixAlertAttemptStatus.Failed;
                    attempt.FinishedAtUtc = DateTime.UtcNow;
                    attempt.ErrorCode = ex is ZabbixGatewayException gatewayException
                        ? gatewayException.Code
                        : ZabbixGatewayMessageCodes.AlertDispatchFailed;
                    attempt.Error = "The telephone dispatch could not be started.";
                    failedDispatches++;

                    _logger.LogError(
                        ex,
                        "zabbix alert dispatch failed to start: alert={alertid}, integration={id}, attempt={attemptnumber}, destination={destinationid}, callDispatchId={calldispatchid}",
                        execution.Id,
                        integration.Id,
                        attempt.AttemptNumber,
                        destination.Id,
                        callDispatchConfiguration.Id);
                }

                await _provider.UpdateAttempt(attempt, cancellationToken);
            }

            if (startedDispatches > 0)
            {
                execution.Status = ZabbixAlertExecutionStatus.Running;
                execution.ErrorCode = null;
                execution.Error = null;
                message = failedDispatches > 0
                    ? $"started {startedDispatches} call dispatch execution(s) and {failedDispatches} failed immediately"
                    : $"started {startedDispatches} call dispatch execution(s)";
            }
            else
            {
                execution.Status = ZabbixAlertExecutionStatus.Failed;
                execution.FinishedAtUtc = DateTime.UtcNow;
                execution.ErrorCode = ZabbixGatewayMessageCodes.AlertDispatchFailed;
                execution.Error = "failed to start any call dispatch execution";
                message = "call dispatch kickoff failed";
            }

            await _provider.UpdateExecution(execution, cancellationToken);

            _logger.LogInformation(
                "zabbix alert start processed: alert={alertid}, integration={id}, context={contextid}, identifier={identifier}, defaultIdentifier={defaultidentifier}, destinations={destinations}, startedDispatches={starteddispatches}, failedDispatches={faileddispatches}, sourceEventId={sourceeventid}",
                alertId,
                integration.Id,
                contextId,
                identifier,
                usesDefaultIdentifier,
                enabledDestinations.Count,
                startedDispatches,
                failedDispatches,
                request?.SourceEventId);

            return new ZabbixAlertStartResult
            {
                Accepted = true,
                AlertId = alertId,
                ContextId = contextId,
                Id = integration.Id,
                Identifier = identifier,
                Digit = integration.Digit,
                UsesDefaultIdentifier = usesDefaultIdentifier,
                ValidatedDestinationCount = enabledDestinations.Count,
                Status = execution.Status,
                MessageCode = failedDispatches == 0
                    ? ZabbixGatewayMessageCodes.AlertDispatchStarted
                    : startedDispatches > 0
                        ? ZabbixGatewayMessageCodes.AlertDispatchPartiallyStarted
                        : ZabbixGatewayMessageCodes.AlertDispatchFailed,
                Message = message,
            };
        }

        /// <summary>
        /// Loads an integration and enforces that it belongs to the informed context and is enabled.
        /// Used by <see cref="StartAlertAsync(System.Guid, System.Guid, ZabbixAlertStartRequest?, System.Threading.CancellationToken)"/> before any execution is persisted.
        /// </summary>
        public async Task<ZabbixGatewayIntegration> GetActiveIntegrationAsync(Guid contextId, Guid id, CancellationToken cancellationToken = default)
        {
            var integration = await _provider.GetByIdAsync(id, cancellationToken)
                ?? throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.IntegrationNotFound,
                    ZabbixGatewayErrorKind.NotFound,
                    $"Zabbix integration not found: {id}.");

            if (integration.ContextId != contextId)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.IntegrationContextMismatch,
                    ZabbixGatewayErrorKind.Forbidden,
                    "The Zabbix integration does not belong to the supplied tenant context.");
            }

            if (!integration.Enabled)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.IntegrationDisabled,
                    ZabbixGatewayErrorKind.Conflict,
                    "The Zabbix integration is disabled.");
            }

            return integration;
        }

        /// <summary>
        /// Reconciles one persisted Zabbix alert with the terminal state of its child
        /// Call Dispatch executions and persists any resulting lifecycle changes.
        /// </summary>
        /// <param name="execution">Zabbix execution previously loaded from persistence.</param>
        /// <param name="cancellationToken">Cancellation token for telephony and persistence operations.</param>
        /// <returns>The supplied execution updated to its current observable state.</returns>
        public async Task<ZabbixAlertExecution> ReconcileExecutionAsync(
            ZabbixAlertExecution execution,
            CancellationToken cancellationToken = default)
        {
            if (execution == null)
                throw new ArgumentNullException(nameof(execution));

            var attempts = (await _provider.ListAttempts(execution.Id, cancellationToken)).ToList();
            if (attempts.Count == 0)
                return execution;

            foreach (var attempt in attempts)
            {
                if (!attempt.DispatchId.HasValue
                    || attempt.DispatchId.Value == Guid.Empty
                    || IsTerminal(attempt.Status))
                {
                    continue;
                }

                var dispatch = await _telephony.GetCallDispatchExecutionAsync(
                    attempt.DispatchId.Value,
                    cancellationToken);
                if (dispatch == null)
                    continue;

                var attemptChanged = false;
                switch (dispatch.Status)
                {
                    case CallDispatchExecutionStatus.Pending:
                    case CallDispatchExecutionStatus.Running:
                        if (attempt.Status != ZabbixAlertAttemptStatus.Running)
                        {
                            attempt.Status = ZabbixAlertAttemptStatus.Running;
                            attemptChanged = true;
                        }
                        break;

                    case CallDispatchExecutionStatus.Completed:
                        if (IsConfirmedDelivery(dispatch))
                        {
                            CompleteAttempt(attempt, dispatch);
                        }
                        else
                        {
                            FailAttempt(
                                attempt,
                                dispatch,
                                ZabbixGatewayMessageCodes.TelephoneDeliveryNotConfirmed,
                                "The telephone worker accepted the request but did not confirm delivery.");
                        }

                        attemptChanged = true;
                        break;

                    case CallDispatchExecutionStatus.Failed:
                        FailAttempt(
                            attempt,
                            dispatch,
                            ResolveTelephoneFailureCode(dispatch),
                            ResolveTelephoneFailureMessage(dispatch));
                        attemptChanged = true;
                        break;
                }

                if (attemptChanged)
                    await _provider.UpdateAttempt(attempt, cancellationToken);
            }

            var previousStatus = execution.Status;
            var previousFinishedAtUtc = execution.FinishedAtUtc;
            var previousErrorCode = execution.ErrorCode;
            var previousError = execution.Error;

            if (attempts.Any(attempt => !IsTerminal(attempt.Status)))
            {
                execution.Status = ZabbixAlertExecutionStatus.Running;
                execution.FinishedAtUtc = null;
                execution.ErrorCode = null;
                execution.Error = null;
            }
            else if (attempts.Any(attempt => attempt.Status == ZabbixAlertAttemptStatus.Completed))
            {
                execution.Status = ZabbixAlertExecutionStatus.Completed;
                execution.FinishedAtUtc = attempts.Max(attempt => attempt.FinishedAtUtc) ?? DateTime.UtcNow;
                execution.ErrorCode = null;
                execution.Error = null;
            }
            else
            {
                var primaryFailure = attempts
                    .Where(attempt => attempt.Status == ZabbixAlertAttemptStatus.Failed)
                    .OrderBy(attempt => attempt.Priority)
                    .ThenBy(attempt => attempt.AttemptNumber)
                    .FirstOrDefault();

                execution.Status = ZabbixAlertExecutionStatus.Failed;
                execution.FinishedAtUtc = attempts.Max(attempt => attempt.FinishedAtUtc) ?? DateTime.UtcNow;
                execution.ErrorCode = primaryFailure?.ErrorCode
                    ?? ZabbixGatewayMessageCodes.TelephoneDeliveryFailed;
                execution.Error = primaryFailure?.Error
                    ?? "The telephone alert could not be delivered.";
            }

            if (previousStatus != execution.Status
                || previousFinishedAtUtc != execution.FinishedAtUtc
                || !string.Equals(previousErrorCode, execution.ErrorCode, StringComparison.Ordinal)
                || !string.Equals(previousError, execution.Error, StringComparison.Ordinal))
            {
                await _provider.UpdateExecution(execution, cancellationToken);

                _logger.LogInformation(
                    "zabbix alert reconciled: alert={alertid}, status={status}, errorCode={errorcode}",
                    execution.Id,
                    execution.Status,
                    execution.ErrorCode);
            }

            return execution;
        }

        /// <summary>
        /// Validates that an optional outbound identifier resolves to a DID owned by the same context.
        /// Returning <see langword="null"/> means the workflow must use the configured default identifier path.
        /// </summary>
        public async Task<DirectInwardDialing?> ValidateIdentifierAsync(Guid contextId, string? identifier, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return null;

            // Tollfree numbers are inbound-only and must never be accepted as outbound caller ID.
            if (_telephony.IsTollFreeIdentifier(identifier))
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.TollFreeIdentifierNotAllowed,
                    ZabbixGatewayErrorKind.Validation,
                    "A toll-free number cannot be used as an outbound caller identifier.");
            }

            var did = await _telephony.GetIdentifierAsync(identifier, cancellationToken)
                ?? throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.DidIdentifierNotFound,
                    ZabbixGatewayErrorKind.NotFound,
                    $"Outbound DID identifier not found: {identifier}.");

            if (did.ContextId != contextId)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.DidIdentifierContextMismatch,
                    ZabbixGatewayErrorKind.Forbidden,
                    "The outbound DID identifier does not belong to the supplied tenant context.");
            }

            return did;
        }

        /// <summary>
        /// Builds a normalized anti-flapping fingerprint from the most stable alert descriptors.
        /// The persisted key will later be used to decide suppression windows across repeated alerts.
        /// </summary>
        private static string? BuildFlapKey(ZabbixAlertStartRequest? request)
        {
            var parts = new[]
            {
                NormalizeOptionalText(request?.Host)?.ToLowerInvariant(),
                NormalizeOptionalText(request?.Trigger)?.ToLowerInvariant(),
                NormalizeOptionalText(request?.Severity)?.ToLowerInvariant(),
            }
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToArray();

            if (parts.Length == 0)
                return null;

            return string.Join(FlapKeySeparator, parts);
        }

        /// <summary>
        /// Trims a user-provided string and converts blank values to <see langword="null"/>.
        /// Used before persisting request data so DB rows and JSON payloads do not keep whitespace-only values.
        /// </summary>
        private static string? NormalizeOptionalText(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        internal static string ResolveTelephoneFailureCode(CallDispatchExecution dispatch)
        {
            var details = $"{dispatch.Message} {dispatch.Error}".ToUpperInvariant();
            if (details.Contains("CHANUNAVAIL")
                || details.Contains("NO TELEPHONE ROUTE")
                || details.Contains("NO TRUNK")
                || details.Contains("ROUTE OR TRUNK")
                || details.Contains("NO ROUTE")
                || details.Contains("HANGUP CAUSE: 3"))
            {
                return ZabbixGatewayMessageCodes.TelephoneRouteUnavailable;
            }

            if (details.Contains("NOANSWER") || details.Contains("DID NOT ANSWER"))
                return ZabbixGatewayMessageCodes.TelephoneDestinationNoAnswer;

            if (details.Contains("BUSY"))
                return ZabbixGatewayMessageCodes.TelephoneDestinationBusy;

            if (details.Contains("CANCEL") || details.Contains("ABORT"))
                return ZabbixGatewayMessageCodes.TelephoneAttemptCanceled;

            if (details.Contains("TIMEOUT"))
                return ZabbixGatewayMessageCodes.TelephoneResultTimedOut;

            if (details.Contains("CONGESTION"))
                return ZabbixGatewayMessageCodes.TelephoneNetworkCongestion;

            return ZabbixGatewayMessageCodes.TelephoneDeliveryFailed;
        }

        internal static string ResolveTelephoneFailureMessage(CallDispatchExecution dispatch)
            => ResolveTelephoneFailureCode(dispatch) switch
            {
                ZabbixGatewayMessageCodes.TelephoneRouteUnavailable =>
                    "No telephone route or trunk was available for the destination.",
                ZabbixGatewayMessageCodes.TelephoneDestinationBusy =>
                    "The telephone destination was busy.",
                ZabbixGatewayMessageCodes.TelephoneDestinationNoAnswer =>
                    "The telephone destination did not answer.",
                ZabbixGatewayMessageCodes.TelephoneAttemptCanceled =>
                    "The telephone dialing attempt was canceled or aborted before delivery.",
                ZabbixGatewayMessageCodes.TelephoneResultTimedOut =>
                    "The telephone worker did not receive a terminal dialing result before timeout.",
                ZabbixGatewayMessageCodes.TelephoneNetworkCongestion =>
                    "The telephone network reported congestion.",
                _ => "The telephone alert could not be delivered.",
            };

        internal static bool IsConfirmedDelivery(CallDispatchExecution dispatch)
            => dispatch.Status == CallDispatchExecutionStatus.Completed
                && !string.IsNullOrWhiteSpace(dispatch.Message)
                && dispatch.Message.IndexOf("answered the call", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsTerminal(ZabbixAlertAttemptStatus status)
            => status == ZabbixAlertAttemptStatus.Completed
                || status == ZabbixAlertAttemptStatus.Failed
                || status == ZabbixAlertAttemptStatus.Canceled;

        private static void CompleteAttempt(
            ZabbixAlertAttempt attempt,
            CallDispatchExecution dispatch)
        {
            attempt.Status = ZabbixAlertAttemptStatus.Completed;
            attempt.FinishedAtUtc = dispatch.FinishedAtUtc ?? DateTime.UtcNow;
            attempt.ErrorCode = null;
            attempt.Error = null;
        }

        private static void FailAttempt(
            ZabbixAlertAttempt attempt,
            CallDispatchExecution dispatch,
            string errorCode,
            string error)
        {
            attempt.Status = ZabbixAlertAttemptStatus.Failed;
            attempt.FinishedAtUtc = dispatch.FinishedAtUtc ?? DateTime.UtcNow;
            attempt.ErrorCode = errorCode;
            attempt.Error = error;
        }

        private async Task<CallDispatchConfiguration> GetCallDispatchConfigurationAsync(Guid contextId, Guid callDispatchId, CancellationToken cancellationToken)
        {
            var configuration = await _telephony.GetCallDispatchConfigurationAsync(callDispatchId, cancellationToken)
                ?? throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.CallDispatchNotFound,
                    ZabbixGatewayErrorKind.NotFound,
                    $"Call Dispatch configuration not found: {callDispatchId}.");

            if (configuration.ContextId != contextId)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.CallDispatchContextMismatch,
                    ZabbixGatewayErrorKind.Forbidden,
                    "The Call Dispatch configuration does not belong to the supplied tenant context.");
            }

            if (string.IsNullOrWhiteSpace(configuration.Asterisk))
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.CallDispatchAsteriskRequired,
                    ZabbixGatewayErrorKind.Conflict,
                    "The Call Dispatch configuration has no Asterisk target.");
            }

            return configuration;
        }

        private static CallDispatchRequest BuildCallDispatchRequest(
            Guid contextId,
            Guid alertId,
            ZabbixGatewayIntegration integration,
            CallDispatchConfiguration callDispatchConfiguration,
            ZabbixGatewayDestination destination,
            ZabbixAlertStartRequest? request)
            => new()
            {
                ContextId = contextId,
                CallDispatchId = callDispatchConfiguration.Id,
                Destination = destination.PhoneNumber,
                ExternalId = alertId.ToString("N"),
                Label = BuildDispatchLabel(integration, callDispatchConfiguration, request),
            };

        private static string BuildDispatchLabel(
            ZabbixGatewayIntegration integration,
            CallDispatchConfiguration callDispatchConfiguration,
            ZabbixAlertStartRequest? request)
        {
            var label = NormalizeOptionalText(request?.Subject)
                ?? NormalizeOptionalText(request?.Trigger)
                ?? NormalizeOptionalText(request?.Host)
                ?? NormalizeOptionalText(callDispatchConfiguration.Title)
                ?? NormalizeOptionalText(integration.Title)
                ?? "Zabbix";

            if (label.Length <= MaxDispatchLabelLength)
                return label;

            return label.Substring(0, MaxDispatchLabelLength);
        }

        private static void ValidateDigit(uint? digit)
        {
            if (!digit.HasValue)
                return;

            if (digit.Value < ZabbixGatewayIntegration.MinimumDigit || digit.Value > ZabbixGatewayIntegration.MaximumDigit)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.ConfirmationDigitInvalid,
                    ZabbixGatewayErrorKind.Validation,
                    $"The confirmation digit must be between {ZabbixGatewayIntegration.MinimumDigit} and {ZabbixGatewayIntegration.MaximumDigit}.");
            }
        }
    }
}
