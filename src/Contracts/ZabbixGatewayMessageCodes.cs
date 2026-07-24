namespace Sufficit.Gateway.Zabbix
{
    /// <summary>
    /// Stable, project-unique message codes emitted by the Sufficit Zabbix gateway.
    /// Frontends must use these codes as localization keys and treat the English message
    /// returned by the backend only as a technical fallback.
    /// </summary>
    public static class ZabbixGatewayMessageCodes
    {
        /// <summary>Stored automation settings are available and should be tested again.</summary>
        public const string StoredAutomationAvailable = "SGZ1001";

        /// <summary>The automation still needs a Zabbix URL and API token.</summary>
        public const string AutomationSetupRequired = "SGZ1002";

        /// <summary>The connection was validated with permission to configure Zabbix.</summary>
        public const string ConnectionValidated = "SGZ1003";

        /// <summary>The connection was validated, but its token cannot configure Zabbix.</summary>
        public const string ConnectionValidatedWithoutPermission = "SGZ1004";

        /// <summary>The managed webhook, user media and trigger action were configured.</summary>
        public const string AutomationConfigured = "SGZ1005";

        /// <summary>Automatic provisioning requires a Zabbix Super Admin token.</summary>
        public const string SuperAdminTokenRequired = "SGZ2001";

        /// <summary>The supplied Zabbix API token exceeds the accepted length.</summary>
        public const string ApiTokenTooLong = "SGZ2002";

        /// <summary>A Zabbix API token must be supplied because no stored token is available.</summary>
        public const string ApiTokenRequired = "SGZ2003";

        /// <summary>The protected token stored for the integration could not be read.</summary>
        public const string StoredApiTokenUnreadable = "SGZ2004";

        /// <summary>Zabbix did not return its API version.</summary>
        public const string ApiVersionMissing = "SGZ2005";

        /// <summary>Zabbix returned an invalid token-owner response.</summary>
        public const string TokenOwnerResponseInvalid = "SGZ2006";

        /// <summary>The user that owns the Zabbix API token was not found.</summary>
        public const string TokenOwnerNotFound = "SGZ2007";

        /// <summary>Zabbix returned a non-success HTTP status.</summary>
        public const string ZabbixHttpError = "SGZ2008";

        /// <summary>Zabbix returned a JSON-RPC API error.</summary>
        public const string ZabbixApiError = "SGZ2009";

        /// <summary>Zabbix returned a JSON-RPC response without a result.</summary>
        public const string ZabbixResultMissing = "SGZ2010";

        /// <summary>The response returned by Zabbix exceeded the safety limit.</summary>
        public const string ZabbixResponseTooLarge = "SGZ2011";

        /// <summary>A Zabbix integration identifier is required.</summary>
        public const string IntegrationIdRequired = "SGZ2012";

        /// <summary>The requested Zabbix integration was not found.</summary>
        public const string IntegrationNotFound = "SGZ2013";

        /// <summary>The supplied Zabbix API URL is not a valid HTTPS URL.</summary>
        public const string ApiUrlInvalid = "SGZ2014";

        /// <summary>The supplied Zabbix API URL does not use a public host.</summary>
        public const string PublicHostRequired = "SGZ2015";

        /// <summary>The Zabbix host name could not be resolved.</summary>
        public const string HostResolutionFailed = "SGZ2016";

        /// <summary>The supplied Zabbix API URL resolves to a private or reserved address.</summary>
        public const string PublicAddressRequired = "SGZ2017";

        /// <summary>A required property is missing from a Zabbix response.</summary>
        public const string ResponsePropertyMissing = "SGZ2018";

        /// <summary>A required identifier in a Zabbix response is empty.</summary>
        public const string ResponseIdentifierEmpty = "SGZ2019";

        /// <summary>A property in a Zabbix response has an invalid value.</summary>
        public const string ResponsePropertyInvalid = "SGZ2020";

        /// <summary>The Zabbix HTTP request could not be completed.</summary>
        public const string ZabbixRequestFailed = "SGZ2021";

        /// <summary>The Zabbix HTTP request exceeded its time limit.</summary>
        public const string ZabbixRequestTimedOut = "SGZ2022";

        /// <summary>Zabbix returned malformed JSON.</summary>
        public const string ZabbixResponseInvalid = "SGZ2023";

        /// <summary>The alert was accepted, but the integration has no enabled destinations.</summary>
        public const string AlertAcceptedWithoutDestinations = "SGZ3001";

        /// <summary>The alert was accepted, but the integration has no Call Dispatch configuration.</summary>
        public const string AlertAcceptedWithoutCallDispatch = "SGZ3002";

        /// <summary>The alert was persisted, but Call Dispatch configuration validation failed.</summary>
        public const string CallDispatchValidationFailed = "SGZ3003";

        /// <summary>Some telephone dispatches started and others failed immediately.</summary>
        public const string AlertDispatchPartiallyStarted = "SGZ3004";

        /// <summary>All requested telephone dispatches started.</summary>
        public const string AlertDispatchStarted = "SGZ3005";

        /// <summary>No telephone dispatch could be started.</summary>
        public const string AlertDispatchFailed = "SGZ3006";

        /// <summary>A tenant context identifier is required.</summary>
        public const string ContextIdRequired = "SGZ3101";

        /// <summary>The integration does not belong to the supplied tenant context.</summary>
        public const string IntegrationContextMismatch = "SGZ3102";

        /// <summary>The Zabbix integration is disabled.</summary>
        public const string IntegrationDisabled = "SGZ3103";

        /// <summary>A toll-free number cannot be used as an outbound caller identifier.</summary>
        public const string TollFreeIdentifierNotAllowed = "SGZ3104";

        /// <summary>The requested outbound DID identifier was not found.</summary>
        public const string DidIdentifierNotFound = "SGZ3105";

        /// <summary>The outbound DID identifier does not belong to the supplied tenant context.</summary>
        public const string DidIdentifierContextMismatch = "SGZ3106";

        /// <summary>The requested Call Dispatch configuration was not found.</summary>
        public const string CallDispatchNotFound = "SGZ3107";

        /// <summary>The Call Dispatch configuration does not belong to the supplied tenant context.</summary>
        public const string CallDispatchContextMismatch = "SGZ3108";

        /// <summary>The Call Dispatch configuration has no Asterisk target.</summary>
        public const string CallDispatchAsteriskRequired = "SGZ3109";

        /// <summary>The confirmation digit is outside the accepted range.</summary>
        public const string ConfirmationDigitInvalid = "SGZ3110";

        /// <summary>The authenticated caller cannot access the requested tenant context.</summary>
        public const string ContextAccessDenied = "SGZ3111";

        /// <summary>No telephone route or trunk was available for the destination.</summary>
        public const string TelephoneRouteUnavailable = "SGZ3201";

        /// <summary>The telephone destination was busy.</summary>
        public const string TelephoneDestinationBusy = "SGZ3202";

        /// <summary>The telephone destination did not answer.</summary>
        public const string TelephoneDestinationNoAnswer = "SGZ3203";

        /// <summary>The telephone dialing attempt was canceled or aborted before delivery.</summary>
        public const string TelephoneAttemptCanceled = "SGZ3204";

        /// <summary>The telephone worker did not receive a terminal dialing result before timeout.</summary>
        public const string TelephoneResultTimedOut = "SGZ3205";

        /// <summary>The telephone network reported congestion.</summary>
        public const string TelephoneNetworkCongestion = "SGZ3206";

        /// <summary>A legacy telephone worker accepted the request but did not confirm delivery.</summary>
        public const string TelephoneDeliveryNotConfirmed = "SGZ3207";

        /// <summary>The internal Asterisk route required to originate the telephone alert does not exist.</summary>
        public const string TelephoneInternalRouteMissing = "SGZ3208";

        /// <summary>The Asterisk manager rejected a legacy call request before dialing started.</summary>
        public const string TelephoneManagerRequestRejected = "SGZ3209";

        /// <summary>The telephone dispatch ended with an unmapped delivery failure.</summary>
        public const string TelephoneDeliveryFailed = "SGZ3299";

        /// <summary>An entity cannot be persisted because its primary-key metadata is missing.</summary>
        public const string EntityPrimaryKeyMissing = "SGZ4001";

        /// <summary>The database connection string required by the gateway is missing.</summary>
        public const string DatabaseConnectionMissing = "SGZ4002";

        /// <summary>The configured public alert callback endpoint is missing or unsafe.</summary>
        public const string PublicAlertEndpointInvalid = "SGZ5001";

        /// <summary>The HTTP request could not be bound to a valid Zabbix gateway contract.</summary>
        public const string RequestInvalid = "SGZ6001";

        /// <summary>The automation API route is not available in the current environment.</summary>
        public const string AutomationApiUnavailable = "SGZ6002";

        /// <summary>The automation API returned no connection status.</summary>
        public const string AutomationStatusMissing = "SGZ6003";

        /// <summary>The automation API returned no configuration result.</summary>
        public const string AutomationResultMissing = "SGZ6004";

        /// <summary>An unexpected error escaped the Zabbix gateway error boundary.</summary>
        public const string UnexpectedError = "SGZ9999";
    }
}
