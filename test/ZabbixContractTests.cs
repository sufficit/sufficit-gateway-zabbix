using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Sufficit.Gateway.Zabbix;
using Sufficit.Gateway.Zabbix.EntityFramework;
using Sufficit.Telephony.CallDispatch;

namespace Sufficit.Gateway.Zabbix.Tests;

public sealed class ZabbixContractTests
{
    [Fact]
    public void IntegrationSerialization_NeverExposesProtectedToken()
    {
        var integration = new ZabbixGatewayIntegration
        {
            Id = Guid.NewGuid(),
            ZabbixApiUrl = "https://zabbix.example.com/api_jsonrpc.php",
            ZabbixApiTokenProtected = "protected-secret-value"
        };

        var json = JsonSerializer.Serialize(integration);

        Assert.Contains("\"zabbix_api_url\"", json);
        Assert.DoesNotContain("protected-secret-value", json);
        Assert.DoesNotContain("zabbix_api_token", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IntegrationSerialization_ExposesServerComputedCallbackWithoutPersistingIt()
    {
        const string callback =
            "https://alerts.example.com/gateway/zabbix/alert?contextId=11111111-1111-1111-1111-111111111111&id=22222222-2222-2222-2222-222222222222";
        var property = typeof(ZabbixGatewayIntegration)
            .GetProperty(nameof(ZabbixGatewayIntegration.AlertCallbackUrl));
        var integration = new ZabbixGatewayIntegration
        {
            Id = Guid.NewGuid(),
            AlertCallbackUrl = callback,
        };

        var json = JsonSerializer.Serialize(integration);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            callback,
            document.RootElement.GetProperty("alert_callback_url").GetString());
        Assert.NotNull(property);
        Assert.NotNull(property!.GetCustomAttribute<NotMappedAttribute>());
    }

    [Theory]
    [InlineData(ZabbixFlapMode.AlwaysCall)]
    [InlineData(ZabbixFlapMode.SuppressWindow)]
    public void IntegrationDefaults_AcceptSupportedFlapModes(ZabbixFlapMode mode)
    {
        var integration = new ZabbixGatewayIntegration { FlapMode = mode };

        Assert.Equal(mode, integration.FlapMode);
        Assert.InRange(integration.ZabbixMinimumSeverity, 0, 5);
    }

    [Fact]
    public void GatewayOptions_BindsConfiguredEndpointAndBuildsTenantCallback()
    {
        var contextId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var integrationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        using var provider = BuildOptionsProvider(
            "https://alerts.example.com/gateway/zabbix/alert");

        var options = provider
            .GetRequiredService<IOptions<ZabbixGatewayOptions>>()
            .Value;

        Assert.Equal(
            "https://alerts.example.com/gateway/zabbix/alert?contextId=11111111-1111-1111-1111-111111111111&id=22222222-2222-2222-2222-222222222222",
            options.BuildAlertCallbackUrl(contextId, integrationId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://alerts.example.com/gateway/zabbix/alert")]
    [InlineData("https://alerts.example.com/gateway/zabbix/alert?fixed=true")]
    [InlineData("https://alerts.example.com/gateway/zabbix/alert#section")]
    public void GatewayOptions_RejectsMissingOrUnsafeEndpoint(string endpoint)
    {
        var exception = Assert.Throws<ZabbixGatewayException>(() => BuildOptionsProvider(endpoint));

        Assert.Equal(ZabbixGatewayMessageCodes.PublicAlertEndpointInvalid, exception.Code);
        Assert.Equal(ZabbixGatewayErrorKind.Configuration, exception.Kind);
        Assert.Equal("SGZ5001", exception.Code);
    }

    [Fact]
    public void MessageCodes_AreUniqueStableStrings()
    {
        var codes = typeof(ZabbixGatewayMessageCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && !field.IsInitOnly)
            .Select(field => Assert.IsType<string>(field.GetRawConstantValue()))
            .ToArray();

        Assert.NotEmpty(codes);
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.Matches(new Regex("^SGZ[0-9]{4}$"), code));
    }

    [Fact]
    public void CommunicationContracts_SerializeStableMessageCodes()
    {
        var status = new ZabbixAutomationStatus
        {
            Id = Guid.NewGuid(),
            MessageCode = ZabbixGatewayMessageCodes.ConnectionValidated,
            Message = "Connection validated."
        };
        var result = new ZabbixAlertStartResult
        {
            AlertId = Guid.NewGuid(),
            MessageCode = ZabbixGatewayMessageCodes.AlertDispatchStarted,
            Message = "The telephone dispatches started."
        };

        var statusJson = JsonSerializer.Serialize(status);
        var resultJson = JsonSerializer.Serialize(result);

        Assert.Contains("\"message_code\":\"SGZ1003\"", statusJson);
        Assert.Contains("\"message_code\":\"SGZ3005\"", resultJson);
    }

    [Theory]
    [InlineData("telephone delivery failed (CHANUNAVAIL)", null, ZabbixGatewayMessageCodes.TelephoneRouteUnavailable)]
    [InlineData("telephone delivery failed (BUSY)", null, ZabbixGatewayMessageCodes.TelephoneDestinationBusy)]
    [InlineData("telephone delivery failed (NOANSWER)", null, ZabbixGatewayMessageCodes.TelephoneDestinationNoAnswer)]
    [InlineData("telephone delivery failed (CANCEL)", null, ZabbixGatewayMessageCodes.TelephoneAttemptCanceled)]
    [InlineData("telephone delivery failed (TIMEOUT)", null, ZabbixGatewayMessageCodes.TelephoneResultTimedOut)]
    [InlineData("telephone delivery failed (CONGESTION)", null, ZabbixGatewayMessageCodes.TelephoneNetworkCongestion)]
    [InlineData("telephone delivery failed (HANGUP)", "Asterisk hangup cause: 3 (No route to destination).", ZabbixGatewayMessageCodes.TelephoneRouteUnavailable)]
    [InlineData("the manager response indicates a failure", "Extension does not exist.", ZabbixGatewayMessageCodes.TelephoneInternalRouteMissing)]
    [InlineData("telephone delivery failed (UNKNOWN)", "unmapped error", ZabbixGatewayMessageCodes.TelephoneDeliveryFailed)]
    public void TelephoneFailures_MapToStableLocalizationCodes(
        string message,
        string? error,
        string expectedCode)
    {
        var dispatch = new CallDispatchExecution
        {
            Status = CallDispatchExecutionStatus.Failed,
            Message = message,
            Error = error,
        };

        Assert.Equal(
            expectedCode,
            ZabbixGatewayService.ResolveTelephoneFailureCode(dispatch));
        Assert.False(
            string.IsNullOrWhiteSpace(
                ZabbixGatewayService.ResolveTelephoneFailureMessage(dispatch)));
    }

    [Theory]
    [InlineData(CallDispatchExecutionStatus.Completed, "The telephone destination answered the call.", true)]
    [InlineData(CallDispatchExecutionStatus.Completed, "Success", false)]
    [InlineData(CallDispatchExecutionStatus.Failed, "The telephone destination answered the call.", false)]
    public void DeliveryConfirmation_RejectsLegacyAmiAcknowledgements(
        CallDispatchExecutionStatus status,
        string message,
        bool expected)
    {
        var dispatch = new CallDispatchExecution
        {
            Status = status,
            Message = message,
        };

        Assert.Equal(expected, ZabbixGatewayService.IsConfirmedDelivery(dispatch));
    }

    [Fact]
    public void GatewayException_PreservesCodeKindAndEnglishFallback()
    {
        var exception = new ZabbixGatewayException(
            ZabbixGatewayMessageCodes.ApiTokenRequired,
            ZabbixGatewayErrorKind.Validation,
            "A Zabbix API token is required.");

        Assert.Equal("SGZ2003", exception.Code);
        Assert.Equal(ZabbixGatewayErrorKind.Validation, exception.Kind);
        Assert.Equal("A Zabbix API token is required.", exception.Message);
    }

    [Fact]
    public void EntityFrameworkProvider_UsesCanonicalSuffix()
    {
        var providerType = typeof(ZabbixGatewayEFProvider);

        Assert.Equal("ZabbixGatewayEFProvider", providerType.Name);
        Assert.Null(providerType.Assembly.GetType(
            "Sufficit.Gateway.Zabbix.EntityFramework.EFZabbixGatewayProvider"));
    }

    [Fact]
    public void EntityFrameworkModel_PersistsApplicationOwnedStartTimes()
    {
        var options = new DbContextOptionsBuilder<EFZabbixGatewayDBContext>()
            .UseMySql(
                "Server=localhost;Database=dbvoiprt;User=test;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        using var dbContext = new EFZabbixGatewayDBContext(options);

        var executionStart = dbContext.Model
            .FindEntityType(typeof(ZabbixAlertExecution))!
            .FindProperty(nameof(ZabbixAlertExecution.StartedAtUtc))!;
        var attemptStart = dbContext.Model
            .FindEntityType(typeof(ZabbixAlertAttempt))!
            .FindProperty(nameof(ZabbixAlertAttempt.StartedAtUtc))!;
        var executionTimestamp = dbContext.Model
            .FindEntityType(typeof(ZabbixAlertExecution))!
            .FindProperty(nameof(ZabbixAlertExecution.Timestamp))!;

        Assert.Equal(ValueGenerated.Never, executionStart.ValueGenerated);
        Assert.Equal(ValueGenerated.Never, attemptStart.ValueGenerated);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, executionTimestamp.ValueGenerated);
    }

    [Fact]
    public void WritableUserMedia_OmitsReadOnlyZabbixIdentifiers()
    {
        using var source = JsonDocument.Parse(
            """
            {
              "mediaid": "42",
              "userid": "1",
              "mediatypeid": "7",
              "sendto": ["alerts@example.com"],
              "active": "0",
              "severity": "63",
              "period": "1-7,00:00-24:00"
            }
            """);

        var media = ZabbixAutomationService.CreateWritableUserMedia(
            source.RootElement);
        var json = JsonSerializer.Serialize(media);

        Assert.DoesNotContain("mediaid", media.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("userid", media.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("\"mediaid\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"userid\"", json, StringComparison.Ordinal);
        Assert.Equal("7", media["mediatypeid"]);
    }

    [Fact]
    public async Task TestAutomation_PersistsAndReusesProtectedTokenByIntegrationContext()
    {
        var integrationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var contextId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var integration = new ZabbixGatewayIntegration
        {
            Id = integrationId,
            ContextId = contextId,
            ZabbixApiUrl = "https://example.com/api_jsonrpc.php",
        };
        using var services = new ServiceCollection().BuildServiceProvider();
        var provider = new RecordingZabbixProvider(
            services.GetRequiredService<IServiceScopeFactory>(),
            integration);
        var handler = new ZabbixAuthenticationHandler();
        var automation = new ZabbixAutomationService(
            provider,
            new SingleClientFactory(handler),
            new TestTokenProtector(),
            Options.Create(new ZabbixGatewayOptions
            {
                PublicAlertEndpoint =
                    "https://alerts.example.com/gateway/zabbix/alert",
            }),
            NullLogger<ZabbixAutomationService>.Instance);

        await automation.TestAsync(new ZabbixAutomationRequest
        {
            Id = integrationId,
            ApiUrl = "https://example.com",
            ApiToken = "customer-secret",
            MinimumSeverity = 3,
        });
        await automation.TestAsync(new ZabbixAutomationRequest
        {
            Id = integrationId,
            ApiUrl = "https://example.com",
            ApiToken = null,
            MinimumSeverity = 3,
        });

        Assert.Equal(contextId, integration.ContextId);
        Assert.Equal("protected::customer-secret", integration.ZabbixApiTokenProtected);
        Assert.Equal("https://example.com/api_jsonrpc.php", integration.ZabbixApiUrl);
        Assert.Equal(new[] { "customer-secret", "customer-secret" }, handler.CheckedTokens);
    }

    private static ServiceProvider BuildOptionsProvider(string endpoint)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ZabbixGatewayOptions.SectionName}:PublicAlertEndpoint"] = endpoint,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSufficitGatewayZabbixOptions(configuration);
        return services.BuildServiceProvider();
    }

    private sealed class RecordingZabbixProvider : ZabbixGatewayEFProvider
    {
        private readonly ZabbixGatewayIntegration _integration;

        public RecordingZabbixProvider(
            IServiceScopeFactory serviceScopeFactory,
            ZabbixGatewayIntegration integration)
            : base(serviceScopeFactory)
        {
            _integration = integration;
        }

        public override async IAsyncEnumerable<ZabbixGatewayIntegration> Search(
            ZabbixGatewaySearchParameters parameters,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            if (!parameters.Id.HasValue || parameters.Id.Value == _integration.Id)
                yield return _integration;
        }

        public override Task<ZabbixGatewayIntegration> UpdateAutomation(
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
            Assert.Equal(_integration.Id, id);
            _integration.ZabbixApiUrl = apiUrl;
            _integration.ZabbixApiTokenProtected = protectedToken;
            _integration.ZabbixMinimumSeverity = minimumSeverity;
            _integration.ZabbixVersion = version;
            _integration.ZabbixUserId = userId;
            _integration.ZabbixMediaTypeId = mediaTypeId;
            _integration.ZabbixActionId = actionId;
            _integration.ZabbixLastConfiguredAtUtc = lastConfiguredAtUtc;
            return Task.FromResult(_integration);
        }
    }

    private sealed class TestTokenProtector : IZabbixTokenProtector
    {
        public string Protect(string token) => $"protected::{token}";

        public string Unprotect(string protectedToken)
            => protectedToken.StartsWith("protected::", StringComparison.Ordinal)
                ? protectedToken["protected::".Length..]
                : throw new InvalidOperationException("Invalid protected token.");
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public SingleClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
            => new(_handler, disposeHandler: false);
    }

    private sealed class ZabbixAuthenticationHandler : HttpMessageHandler
    {
        public List<string> CheckedTokens { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var json = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            var method = document.RootElement.GetProperty("method").GetString();

            string result;
            if (method == "apiinfo.version")
            {
                result = "\"6.0.22\"";
            }
            else if (method == "user.checkAuthentication")
            {
                CheckedTokens.Add(
                    document.RootElement
                        .GetProperty("params")
                        .GetProperty("token")
                        .GetString()!);
                result =
                    """{"userid":"1","username":"customer-admin","type":"3"}""";
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unexpected Zabbix method in test: {method}");
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"jsonrpc":"2.0","result":{{result}},"id":1}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }
}
