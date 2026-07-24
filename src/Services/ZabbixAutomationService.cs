using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sufficit.Gateway.Zabbix.EntityFramework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Sufficit.Gateway.Zabbix
{
    /// <summary>
    /// Configures the customer-owned Zabbix instance through JSON-RPC while
    /// keeping HTTP, secrets and persistence concerns inside the gateway package.
    /// </summary>
    public sealed class ZabbixAutomationService
    {
        public const string HttpClientName = "Sufficit.Gateway.Zabbix.Automation";
        private const int ZabbixSuperAdminType = 3;
        private const int MaximumTokenLength = 512;
        private const int MaximumResponseBytes = 1024 * 1024;
        private static readonly TimeSpan ZabbixRequestTimeout = TimeSpan.FromSeconds(20);

        private readonly ZabbixGatewayEFProvider _provider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IZabbixTokenProtector _tokenProtector;
        private readonly ZabbixGatewayOptions _options;
        private readonly ILogger<ZabbixAutomationService> _logger;

        public ZabbixAutomationService(
            ZabbixGatewayEFProvider provider,
            IHttpClientFactory httpClientFactory,
            IZabbixTokenProtector tokenProtector,
            IOptions<ZabbixGatewayOptions> options,
            ILogger<ZabbixAutomationService> logger)
        {
            _provider = provider;
            _httpClientFactory = httpClientFactory;
            _tokenProtector = tokenProtector;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<ZabbixAutomationStatus> GetStatusAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var integration = await GetRequiredIntegrationAsync(id, cancellationToken);
            return CreateStoredAutomationStatus(integration);
        }

        public async Task<ZabbixAutomationStatus> TestAsync(
            ZabbixAutomationRequest request,
            CancellationToken cancellationToken = default)
        {
            var connection = await ValidateConnectionAsync(request, cancellationToken);

            await _provider.UpdateAutomation(
                connection.Integration.Id,
                connection.ApiUri.ToString(),
                connection.ProtectedToken,
                connection.MinimumSeverity,
                connection.Version,
                connection.UserId,
                connection.Integration.ZabbixMediaTypeId,
                connection.Integration.ZabbixActionId,
                connection.Integration.ZabbixLastConfiguredAtUtc,
                cancellationToken);

            return new ZabbixAutomationStatus
            {
                Id = connection.Integration.Id,
                ApiUrl = connection.ApiUri.ToString(),
                HasStoredToken = true,
                Connected = true,
                Configured = !string.IsNullOrWhiteSpace(connection.Integration.ZabbixMediaTypeId)
                    && !string.IsNullOrWhiteSpace(connection.Integration.ZabbixActionId),
                CanConfigure = connection.UserType == ZabbixSuperAdminType,
                Version = connection.Version,
                UserName = connection.UserName,
                MinimumSeverity = connection.MinimumSeverity,
                MediaTypeId = connection.Integration.ZabbixMediaTypeId,
                ActionId = connection.Integration.ZabbixActionId,
                LastConfiguredAtUtc = connection.Integration.ZabbixLastConfiguredAtUtc,
                MessageCode = connection.UserType == ZabbixSuperAdminType
                    ? ZabbixGatewayMessageCodes.ConnectionValidated
                    : ZabbixGatewayMessageCodes.ConnectionValidatedWithoutPermission,
                Message = connection.UserType == ZabbixSuperAdminType
                    ? "Connection validated. The token can configure the webhook and trigger action."
                    : "Connection validated, but automatic configuration requires a Super Admin token.",
            };
        }

        public async Task<ZabbixAutomationStatus> ConfigureAsync(
            ZabbixAutomationRequest request,
            CancellationToken cancellationToken = default)
        {
            var connection = await ValidateConnectionAsync(request, cancellationToken);
            if (connection.UserType != ZabbixSuperAdminType)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.SuperAdminTokenRequired,
                    ZabbixGatewayErrorKind.Forbidden,
                    "Automatic configuration requires a Zabbix Super Admin token.");
            }

            var mediaTypeId = await UpsertMediaTypeAsync(connection, cancellationToken);
            await AttachMediaToTokenOwnerAsync(connection, mediaTypeId, cancellationToken);
            var actionId = await UpsertActionAsync(connection, mediaTypeId, cancellationToken);
            var configuredAtUtc = DateTime.UtcNow;

            await _provider.UpdateAutomation(
                connection.Integration.Id,
                connection.ApiUri.ToString(),
                connection.ProtectedToken,
                connection.MinimumSeverity,
                connection.Version,
                connection.UserId,
                mediaTypeId,
                actionId,
                configuredAtUtc,
                cancellationToken);

            _logger.LogInformation(
                "customer zabbix automation configured: integration={integrationid}, version={version}, mediaType={mediatypeid}, action={actionid}",
                connection.Integration.Id,
                connection.Version,
                mediaTypeId,
                actionId);

            return new ZabbixAutomationStatus
            {
                Id = connection.Integration.Id,
                ApiUrl = connection.ApiUri.ToString(),
                HasStoredToken = true,
                Connected = true,
                Configured = true,
                CanConfigure = true,
                Version = connection.Version,
                UserName = connection.UserName,
                MinimumSeverity = connection.MinimumSeverity,
                MediaTypeId = mediaTypeId,
                ActionId = actionId,
                LastConfiguredAtUtc = configuredAtUtc,
                MessageCode = ZabbixGatewayMessageCodes.AutomationConfigured,
                Message = "The webhook, user media and trigger action were configured in Zabbix.",
            };
        }

        private async Task<ZabbixAutomationConnection> ValidateConnectionAsync(
            ZabbixAutomationRequest request,
            CancellationToken cancellationToken)
        {
            var integration = await GetRequiredIntegrationAsync(request.Id, cancellationToken);
            var apiUri = NormalizeApiUri(request.ApiUrl ?? integration.ZabbixApiUrl);
            await EnsurePublicDestinationAsync(apiUri, cancellationToken);

            var suppliedToken = request.ApiToken?.Trim();
            if (suppliedToken?.Length > MaximumTokenLength)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.ApiTokenTooLong,
                    ZabbixGatewayErrorKind.Validation,
                    "The Zabbix API token exceeds the maximum accepted length.");
            }

            string token;
            string protectedToken;
            if (!string.IsNullOrWhiteSpace(suppliedToken))
            {
                token = suppliedToken;
                protectedToken = _tokenProtector.Protect(token);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(integration.ZabbixApiTokenProtected))
                {
                    throw new ZabbixGatewayException(
                        ZabbixGatewayMessageCodes.ApiTokenRequired,
                        ZabbixGatewayErrorKind.Validation,
                        "A Zabbix API token is required.");
                }

                try
                {
                    token = _tokenProtector.Unprotect(integration.ZabbixApiTokenProtected);
                    protectedToken = integration.ZabbixApiTokenProtected;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "failed to unprotect customer zabbix token: integration={integrationid}", integration.Id);
                    throw new ZabbixGatewayException(
                        ZabbixGatewayMessageCodes.StoredApiTokenUnreadable,
                        ZabbixGatewayErrorKind.Conflict,
                        "The stored Zabbix API token could not be read. Supply a new token.",
                        ex);
                }
            }

            var versionResult = await CallZabbixAsync(apiUri, "apiinfo.version", new { }, null, cancellationToken);
            var version = versionResult.GetString();
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.ApiVersionMissing,
                    ZabbixGatewayErrorKind.ExternalService,
                    "Zabbix did not return its API version.");
            }

            var userResult = await CallZabbixAsync(
                apiUri,
                "user.checkAuthentication",
                new { token },
                null,
                cancellationToken);

            var userId = GetRequiredString(userResult, "userid");
            var userName = GetOptionalString(userResult, "username") ?? $"User {userId}";
            var userType = GetRequiredInt32(userResult, "type");

            return new ZabbixAutomationConnection(
                integration,
                apiUri,
                token,
                protectedToken,
                ClampSeverity(request.MinimumSeverity),
                version,
                userId,
                userName,
                userType);
        }

        private async Task<string> UpsertMediaTypeAsync(ZabbixAutomationConnection connection, CancellationToken cancellationToken)
        {
            var name = GetManagedName(connection.Integration.Id);
            var existing = await CallZabbixAsync(
                connection.ApiUri,
                "mediatype.get",
                new
                {
                    output = new[] { "mediatypeid", "name" },
                    filter = new { name },
                },
                connection.Token,
                cancellationToken);

            var mediaTypeId = existing.ValueKind == JsonValueKind.Array
                ? existing.EnumerateArray().Select(item => GetOptionalString(item, "mediatypeid")).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                : null;

            var parameters = new[]
            {
                new
                {
                    name = "URL",
                    value = _options.BuildAlertCallbackUrl(
                        connection.Integration.ContextId,
                        connection.Integration.Id),
                },
                new { name = "SourceEventId", value = "{EVENT.ID}" },
                new { name = "Host", value = "{HOST.HOST}" },
                new { name = "Trigger", value = "{EVENT.NAME}" },
                new { name = "Severity", value = "{EVENT.SEVERITY}" },
                new { name = "Subject", value = "{ALERT.SUBJECT}" },
                new { name = "Message", value = "{ALERT.MESSAGE}" },
            };

            var messageTemplates = new[]
            {
                new
                {
                    eventsource = 0,
                    recovery = 0,
                    subject = "Alert {EVENT.SEVERITY}: {EVENT.NAME}",
                    message = "{EVENT.NAME} on {HOST.HOST}. Severity: {EVENT.SEVERITY}. Value: {ITEM.LASTVALUE}.",
                },
            };

            object payload;
            string method;
            if (string.IsNullOrWhiteSpace(mediaTypeId))
            {
                method = "mediatype.create";
                payload = new
                {
                    type = 4,
                    name,
                    status = 0,
                    description = "Managed by Sufficit. Sends PROBLEM events to the telephone notification gateway.",
                    script = GetWebhookScript(),
                    timeout = "15s",
                    maxattempts = 3,
                    attempt_interval = "10s",
                    parameters,
                    message_templates = messageTemplates,
                };
            }
            else
            {
                method = "mediatype.update";
                payload = new
                {
                    mediatypeid = mediaTypeId,
                    name,
                    status = 0,
                    description = "Managed by Sufficit. Sends PROBLEM events to the telephone notification gateway.",
                    script = GetWebhookScript(),
                    timeout = "15s",
                    maxattempts = 3,
                    attempt_interval = "10s",
                    parameters,
                    message_templates = messageTemplates,
                };
            }

            var result = await CallZabbixAsync(connection.ApiUri, method, payload, connection.Token, cancellationToken);
            return GetFirstId(result, "mediatypeids");
        }

        private async Task AttachMediaToTokenOwnerAsync(
            ZabbixAutomationConnection connection,
            string mediaTypeId,
            CancellationToken cancellationToken)
        {
            var users = await CallZabbixAsync(
                connection.ApiUri,
                "user.get",
                new
                {
                    output = new[] { "userid", "username" },
                    userids = new[] { connection.UserId },
                    selectMedias = "extend",
                },
                connection.Token,
                cancellationToken);

            if (users.ValueKind != JsonValueKind.Array)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.TokenOwnerResponseInvalid,
                    ZabbixGatewayErrorKind.ExternalService,
                    "Zabbix returned an invalid token-owner response.");
            }

            var user = users.EnumerateArray().FirstOrDefault();
            if (user.ValueKind != JsonValueKind.Object)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.TokenOwnerNotFound,
                    ZabbixGatewayErrorKind.NotFound,
                    "The user that owns the Zabbix API token was not found.");
            }

            var medias = new List<Dictionary<string, object?>>();
            if (user.TryGetProperty("medias", out var currentMedias) && currentMedias.ValueKind == JsonValueKind.Array)
            {
                foreach (var media in currentMedias.EnumerateArray())
                    medias.Add(CreateWritableUserMedia(media));
            }

            if (medias.Any(item => string.Equals(item["mediatypeid"]?.ToString(), mediaTypeId, StringComparison.Ordinal)))
                return;

            medias.Add(new Dictionary<string, object?>
            {
                ["mediatypeid"] = mediaTypeId,
                ["sendto"] = new[] { "Sufficit" },
                ["active"] = 0,
                ["severity"] = 63,
                ["period"] = "1-7,00:00-24:00",
            });

            await CallZabbixAsync(
                connection.ApiUri,
                "user.update",
                new
                {
                    userid = connection.UserId,
                    medias,
                },
                connection.Token,
                cancellationToken);
        }

        /// <summary>
        /// Projects a media returned by <c>user.get</c> to the writable shape accepted by
        /// <c>user.update</c>. Read-only identifiers such as <c>mediaid</c> and <c>userid</c>
        /// must not be sent back to Zabbix.
        /// </summary>
        internal static Dictionary<string, object?> CreateWritableUserMedia(JsonElement media)
            => new()
            {
                ["mediatypeid"] = GetRequiredString(media, "mediatypeid"),
                ["sendto"] = ReadSendTo(media),
                ["active"] = GetRequiredInt32(media, "active"),
                ["severity"] = GetRequiredInt32(media, "severity"),
                ["period"] = GetOptionalString(media, "period") ?? "1-7,00:00-24:00",
            };

        private async Task<string> UpsertActionAsync(
            ZabbixAutomationConnection connection,
            string mediaTypeId,
            CancellationToken cancellationToken)
        {
            var name = GetManagedName(connection.Integration.Id);
            var existing = await CallZabbixAsync(
                connection.ApiUri,
                "action.get",
                new
                {
                    output = new[] { "actionid", "name" },
                    filter = new { name },
                },
                connection.Token,
                cancellationToken);

            var actionId = existing.ValueKind == JsonValueKind.Array
                ? existing.EnumerateArray().Select(item => GetOptionalString(item, "actionid")).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                : null;

            var filter = new
            {
                evaltype = 1,
                conditions = new[]
                {
                    new
                    {
                        conditiontype = 4,
                        @operator = 5,
                        value = connection.MinimumSeverity.ToString(),
                    },
                },
            };

            var operations = new[]
            {
                new
                {
                    operationtype = 0,
                    esc_step_from = 1,
                    esc_step_to = 1,
                    opmessage_usr = new[] { new { userid = connection.UserId } },
                    opmessage = new
                    {
                        default_msg = 1,
                        mediatypeid = mediaTypeId,
                    },
                },
            };

            object payload;
            string method;
            if (string.IsNullOrWhiteSpace(actionId))
            {
                method = "action.create";
                payload = new
                {
                    name,
                    eventsource = 0,
                    status = 0,
                    esc_period = "1m",
                    filter,
                    operations,
                };
            }
            else
            {
                method = "action.update";
                payload = new
                {
                    actionid = actionId,
                    name,
                    eventsource = 0,
                    status = 0,
                    esc_period = "1m",
                    filter,
                    operations,
                };
            }

            var result = await CallZabbixAsync(connection.ApiUri, method, payload, connection.Token, cancellationToken);
            return GetFirstId(result, "actionids");
        }

        private async Task<JsonElement> CallZabbixAsync(
            Uri apiUri,
            string method,
            object parameters,
            string? token,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, apiUri)
            {
                Content = JsonContent.Create(new
                {
                    jsonrpc = "2.0",
                    method,
                    @params = parameters,
                    auth = token,
                    id = 1,
                }),
            };

            request.Headers.Accept.ParseAdd("application/json");

            using var client = _httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = ZabbixRequestTimeout;

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.ZabbixRequestTimedOut,
                    ZabbixGatewayErrorKind.ExternalService,
                    "The Zabbix API request timed out.",
                    ex);
            }
            catch (HttpRequestException ex)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.ZabbixRequestFailed,
                    ZabbixGatewayErrorKind.ExternalService,
                    "The Zabbix API request could not be completed.",
                    ex);
            }

            using var responseScope = response;
            if ((int)response.StatusCode is < 200 or >= 300)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.ZabbixHttpError,
                    ZabbixGatewayErrorKind.ExternalService,
                    $"Zabbix returned HTTP {(int)response.StatusCode}.");
            }

            using var responseStream = await ReadLimitedResponseAsync(response, cancellationToken);
            JsonDocument document;
            try
            {
                document = await JsonDocument.ParseAsync(
                    responseStream,
                    cancellationToken: cancellationToken);
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.ZabbixResponseInvalid,
                    ZabbixGatewayErrorKind.ExternalService,
                    "Zabbix returned malformed JSON.",
                    ex);
            }

            using var documentScope = document;
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                var message = GetOptionalString(error, "message") ?? "API error";
                var data = GetOptionalString(error, "data");
                _logger.LogWarning(
                    "zabbix json-rpc request rejected: method={method}, message={message}, data={data}",
                    method,
                    message,
                    data);
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.ZabbixApiError,
                    ZabbixGatewayErrorKind.ExternalService,
                    "Zabbix rejected the JSON-RPC request.");
            }

            if (!root.TryGetProperty("result", out var result))
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.ZabbixResultMissing,
                    ZabbixGatewayErrorKind.ExternalService,
                    "Zabbix returned a JSON-RPC response without a result.");
            }

            return result.Clone();
        }

        private static async Task<MemoryStream> ReadLimitedResponseAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            if (response.Content.Headers.ContentLength > MaximumResponseBytes)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.ZabbixResponseTooLarge,
                    ZabbixGatewayErrorKind.ExternalService,
                    "The Zabbix response exceeded the safety limit.");
            }

#if NET7_0_OR_GREATER
            using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
#else
            using var source = await response.Content.ReadAsStreamAsync();
            cancellationToken.ThrowIfCancellationRequested();
#endif
            var destination = new MemoryStream();
            var buffer = new byte[16 * 1024];

            try
            {
                while (true)
                {
                    var read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (read == 0)
                        break;

                    if (destination.Length + read > MaximumResponseBytes)
                    {
                        throw new ZabbixGatewayException(
                            ZabbixGatewayMessageCodes.ZabbixResponseTooLarge,
                            ZabbixGatewayErrorKind.ExternalService,
                            "The Zabbix response exceeded the safety limit.");
                    }

                    await destination.WriteAsync(buffer, 0, read, cancellationToken);
                }

                destination.Position = 0;
                return destination;
            }
            catch
            {
                destination.Dispose();
                throw;
            }
        }

        private async Task<ZabbixGatewayIntegration> GetRequiredIntegrationAsync(Guid id, CancellationToken cancellationToken)
        {
            if (id == Guid.Empty)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.IntegrationIdRequired,
                    ZabbixGatewayErrorKind.Validation,
                    "A Zabbix integration identifier is required.");
            }

            return await _provider.GetByIdAsync(id, cancellationToken)
                ?? throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.IntegrationNotFound,
                    ZabbixGatewayErrorKind.NotFound,
                    $"Zabbix integration not found: {id}.");
        }

        private static ZabbixAutomationStatus CreateStoredAutomationStatus(ZabbixGatewayIntegration integration)
            => new()
            {
                Id = integration.Id,
                ApiUrl = integration.ZabbixApiUrl,
                HasStoredToken = !string.IsNullOrWhiteSpace(integration.ZabbixApiTokenProtected),
                Connected = false,
                Configured = !string.IsNullOrWhiteSpace(integration.ZabbixMediaTypeId)
                    && !string.IsNullOrWhiteSpace(integration.ZabbixActionId),
                CanConfigure = false,
                Version = integration.ZabbixVersion,
                MinimumSeverity = ClampSeverity(integration.ZabbixMinimumSeverity),
                MediaTypeId = integration.ZabbixMediaTypeId,
                ActionId = integration.ZabbixActionId,
                LastConfiguredAtUtc = integration.ZabbixLastConfiguredAtUtc,
                MessageCode = !string.IsNullOrWhiteSpace(integration.ZabbixActionId)
                    ? ZabbixGatewayMessageCodes.StoredAutomationAvailable
                    : ZabbixGatewayMessageCodes.AutomationSetupRequired,
                Message = !string.IsNullOrWhiteSpace(integration.ZabbixActionId)
                    ? "Stored automation settings are available. Test the connection to validate current access."
                    : "Supply the Zabbix URL and API token to begin.",
            };

        private static Uri NormalizeApiUri(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(uri.DnsSafeHost)
                || !string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.ApiUrlInvalid,
                    ZabbixGatewayErrorKind.Validation,
                    "Supply a valid HTTPS URL for the Zabbix API.");
            }

            var path = uri.AbsolutePath.TrimEnd('/');
            if (!path.EndsWith("/api_jsonrpc.php", StringComparison.OrdinalIgnoreCase))
                path += "/api_jsonrpc.php";

            return new UriBuilder(uri)
            {
                Path = path,
                Query = string.Empty,
                Fragment = string.Empty,
            }.Uri;
        }

        private static async Task EnsurePublicDestinationAsync(Uri uri, CancellationToken cancellationToken)
        {
            if (string.Equals(uri.DnsSafeHost, "localhost", StringComparison.OrdinalIgnoreCase)
                || uri.DnsSafeHost.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.PublicHostRequired,
                    ZabbixGatewayErrorKind.Validation,
                    "The Zabbix API URL must use a public host.");
            }

            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (SocketException ex)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.HostResolutionFailed,
                    ZabbixGatewayErrorKind.Validation,
                    "The Zabbix host name could not be resolved.",
                    ex);
            }

            if (addresses.Length == 0 || addresses.Any(IsPrivateOrReservedAddress))
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.PublicAddressRequired,
                    ZabbixGatewayErrorKind.Validation,
                    "The Zabbix API URL must resolve only to public addresses.");
            }
        }

        private static bool IsPrivateOrReservedAddress(IPAddress address)
        {
            if (IPAddress.IsLoopback(address)
                || address.Equals(IPAddress.Any)
                || address.Equals(IPAddress.IPv6Any)
                || address.IsIPv6LinkLocal
                || address.IsIPv6Multicast
                || address.IsIPv6SiteLocal)
            {
                return true;
            }

            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                var ipv6 = address.GetAddressBytes();
                return ipv6.Length == 16 && (ipv6[0] & 0xfe) == 0xfc;
            }

            var bytes = address.GetAddressBytes();
            return bytes[0] == 0
                || bytes[0] == 10
                || bytes[0] == 127
                || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 0 && (bytes[2] == 0 || bytes[2] == 2))
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 198 && bytes[1] is 18 or 19)
                || (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100)
                || (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113)
                || bytes[0] >= 224;
        }

        private static string GetManagedName(Guid integrationId)
            => $"Sufficit Voice Alerts — {integrationId:D}";

        private static string GetWebhookScript()
            => @"var params = JSON.parse(value);
var request = new HttpRequest();
request.addHeader('Content-Type: application/json');

var payload = {
    source_event_id: params.SourceEventId,
    host: params.Host,
    trigger: params.Trigger,
    severity: params.Severity,
    subject: params.Subject,
    message: params.Message
};

var response = request.post(params.URL, JSON.stringify(payload));
var status = request.getStatus();
if (status < 200 || status >= 300) {
    throw 'Sufficit gateway returned HTTP ' + status + ': ' + response;
}

return JSON.stringify({ status: 'sent', response: response });";

        private static string[] ReadSendTo(JsonElement media)
        {
            if (!media.TryGetProperty("sendto", out var sendTo))
                return new[] { "Sufficit" };

            if (sendTo.ValueKind == JsonValueKind.Array)
            {
                var values = sendTo.EnumerateArray()
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item!)
                    .ToArray();
                return values.Length == 0 ? new[] { "Sufficit" } : values;
            }

            var single = sendTo.GetString();
            return string.IsNullOrWhiteSpace(single) ? new[] { "Sufficit" } : new[] { single };
        }

        private static string GetFirstId(JsonElement result, string property)
        {
            if (!result.TryGetProperty(property, out var ids) || ids.ValueKind != JsonValueKind.Array)
            {
                throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.ResponsePropertyMissing,
                    ZabbixGatewayErrorKind.ExternalService,
                    $"Zabbix did not return the required '{property}' property.");
            }

            var id = ids.EnumerateArray().Select(item => item.GetString()).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            return id ?? throw new ZabbixGatewayException(
                ZabbixGatewayMessageCodes.ResponseIdentifierEmpty,
                ZabbixGatewayErrorKind.ExternalService,
                $"Zabbix returned an empty identifier in '{property}'.");
        }

        private static string GetRequiredString(JsonElement element, string property)
            => GetOptionalString(element, property)
                ?? throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.ResponsePropertyMissing,
                    ZabbixGatewayErrorKind.ExternalService,
                    $"Zabbix did not return the required '{property}' property.");

        private static string? GetOptionalString(JsonElement element, string property)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
                return null;

            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        }

        private static int GetRequiredInt32(JsonElement element, string property)
        {
            var value = GetRequiredString(element, property);
            return int.TryParse(value, out var number)
                ? number
                : throw new ZabbixGatewayException(
                    ZabbixGatewayMessageCodes.ResponsePropertyInvalid,
                    ZabbixGatewayErrorKind.ExternalService,
                    $"Zabbix returned an invalid value for '{property}'.");
        }

        private static int ClampSeverity(int severity)
            => Math.Min(5, Math.Max(0, severity));

        private sealed class ZabbixAutomationConnection
        {
            public ZabbixAutomationConnection(
                ZabbixGatewayIntegration integration,
                Uri apiUri,
                string token,
                string protectedToken,
                int minimumSeverity,
                string version,
                string userId,
                string userName,
                int userType)
            {
                Integration = integration;
                ApiUri = apiUri;
                Token = token;
                ProtectedToken = protectedToken;
                MinimumSeverity = minimumSeverity;
                Version = version;
                UserId = userId;
                UserName = userName;
                UserType = userType;
            }

            public ZabbixGatewayIntegration Integration { get; }

            public Uri ApiUri { get; }

            public string Token { get; }

            public string ProtectedToken { get; }

            public int MinimumSeverity { get; }

            public string Version { get; }

            public string UserId { get; }

            public string UserName { get; }

            public int UserType { get; }
        }
    }
}
