-- Example schema for Zabbix Gateway persistence.
-- Run against the target MySQL/MariaDB database used by the gateway module.

CREATE TABLE IF NOT EXISTS gatw_zabbix_integrations (
    id BINARY(16) NOT NULL COMMENT 'Primary identifier of the Zabbix gateway integration',
    contextid BINARY(16) NOT NULL COMMENT 'Tenant context identifier that owns the integration',
    title VARCHAR(255) NULL COMMENT 'Business title used to identify the integration inside the tenant context',
    enabled BIT(1) NOT NULL DEFAULT b'1' COMMENT 'Whether the integration is active for alert processing',
    flap_mode INT(1) NOT NULL DEFAULT 1 COMMENT 'Flapping policy mode used to decide whether repeated alerts should be suppressed',
    flap_window_seconds INT(11) NOT NULL DEFAULT 120 COMMENT 'Suppression window in seconds applied when the selected flapping mode suppresses repeated alerts',
    identifier VARCHAR(40) NULL COMMENT 'Optional outbound caller identifier selected by the tenant for alert calls; null means use the Sufficit default identifier from configuration',
    digit INT(1) UNSIGNED NULL COMMENT 'Optional DTMF digit required to confirm alert receipt for this integration; null means disabled',
    call_dispatch_id BINARY(16) NULL COMMENT 'Optional Call Dispatch preset used to start outbound alert calls; null keeps validation and persistence without telephony kickoff',
    zabbix_api_url VARCHAR(2048) NULL COMMENT 'Customer-owned Zabbix JSON-RPC endpoint used by guided automation',
    zabbix_api_token_protected TEXT NULL COMMENT 'Customer Zabbix API token protected at rest by ASP.NET Core Data Protection',
    zabbix_minimum_severity INT(1) NOT NULL DEFAULT 3 COMMENT 'Minimum trigger severity configured in the generated Zabbix action (0-5)',
    zabbix_media_type_id VARCHAR(32) NULL COMMENT 'Remote Zabbix media type identifier managed by guided automation',
    zabbix_action_id VARCHAR(32) NULL COMMENT 'Remote Zabbix action identifier managed by guided automation',
    zabbix_user_id VARCHAR(32) NULL COMMENT 'Remote token-owner user identifier receiving the generated webhook media',
    zabbix_version VARCHAR(32) NULL COMMENT 'Last Zabbix API version validated by guided automation',
    zabbix_last_configured_at_utc DATETIME(6) NULL COMMENT 'UTC timestamp of the last successful remote provisioning',
    `update` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'Last update timestamp for the integration row',
    PRIMARY KEY (id),
    UNIQUE KEY uq_gatw_zabbix_integrations_contextid_title (contextid, title)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='Zabbix gateway integrations by tenant context';

CREATE TABLE IF NOT EXISTS gatw_zabbix_destinations (
    id BINARY(16) NOT NULL COMMENT 'Primary identifier of the Zabbix gateway destination',
    integrationid BINARY(16) NOT NULL COMMENT 'Identifier of the parent Zabbix gateway integration',
    contextid BINARY(16) NOT NULL COMMENT 'Tenant context identifier that owns the destination',
    title VARCHAR(255) NULL COMMENT 'Business title used to identify the destination inside the integration',
    phone_number VARCHAR(40) NOT NULL COMMENT 'Phone number or dialable target used for alert calls',
    priority INT(11) NOT NULL DEFAULT 1 COMMENT 'Priority order used when trying destinations for the same integration',
    enabled BIT(1) NOT NULL DEFAULT b'1' COMMENT 'Whether the destination is active for alert delivery',
    `update` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'Last update timestamp for the destination row',
    PRIMARY KEY (id),
    KEY ix_gatw_zabbix_destinations_integrationid_priority_id (integrationid, priority, id),
    CONSTRAINT fk_gatw_zabbix_destinations_integrationid FOREIGN KEY (integrationid)
        REFERENCES gatw_zabbix_integrations (id)
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='Zabbix gateway destinations ordered by delivery priority';

CREATE TABLE IF NOT EXISTS gatw_zabbix_executions (
    id BINARY(16) NOT NULL COMMENT 'Public alert identifier returned when the alert start is accepted',
    contextid BINARY(16) NOT NULL COMMENT 'Tenant context identifier that owns the alert execution',
    integrationid BINARY(16) NOT NULL COMMENT 'Identifier of the Zabbix integration used to start the alert',
    source_event_id VARCHAR(255) NULL COMMENT 'External source event identifier used for idempotency when available',
    host VARCHAR(255) NULL COMMENT 'Host or monitored entity related to the incoming alert',
    `trigger` VARCHAR(500) NULL COMMENT 'Trigger or event expression received from the monitoring system',
    severity VARCHAR(50) NULL COMMENT 'Severity label received from the monitoring system',
    `subject` VARCHAR(500) NULL COMMENT 'Human readable alert subject received from the monitoring system',
    identifier VARCHAR(40) NULL COMMENT 'Resolved outbound caller identifier selected for this alert execution',
    digit INT(1) UNSIGNED NULL COMMENT 'Optional DTMF digit required to confirm alert receipt for this execution; null means disabled',
    call_dispatch_id BINARY(16) NULL COMMENT 'Optional Call Dispatch preset selected for this alert execution; null keeps the flow in validation-only mode',
    uses_default_identifier BIT(1) NOT NULL DEFAULT b'1' COMMENT 'Whether the alert execution used the platform default identifier instead of a tenant DID',
    flap_key VARCHAR(500) NULL COMMENT 'Normalized flap key used to correlate repeated alerts for suppression logic',
    `status` VARCHAR(40) NOT NULL COMMENT 'Current execution status for the alert lifecycle',
    started_at_utc DATETIME(6) NOT NULL COMMENT 'UTC time when the execution record was created',
    finished_at_utc DATETIME(6) NULL COMMENT 'UTC time when the execution reached a terminal state',
    error_code VARCHAR(16) NULL COMMENT 'Stable SGZ localization code for the execution error',
    `error` VARCHAR(500) NULL COMMENT 'English technical fallback stored for the execution error',
    `update` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'Last update timestamp for the execution row',
    PRIMARY KEY (id),
    UNIQUE KEY uq_gatw_zabbix_executions_contextid_integrationid_sourceeventid (contextid, integrationid, source_event_id),
    KEY ix_gatw_zabbix_executions_contextid_integrationid_status_started (contextid, integrationid, status, started_at_utc),
    CONSTRAINT fk_gatw_zabbix_executions_integrationid FOREIGN KEY (integrationid)
        REFERENCES gatw_zabbix_integrations (id)
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='Operational execution records for Zabbix alert processing';

CREATE TABLE IF NOT EXISTS gatw_zabbix_attempts (
    id BINARY(16) NOT NULL COMMENT 'Primary identifier of the alert attempt row',
    alertid BINARY(16) NOT NULL COMMENT 'Execution alert identifier that owns this attempt',
    contextid BINARY(16) NOT NULL COMMENT 'Tenant context identifier that owns the attempt',
    destinationid BINARY(16) NOT NULL COMMENT 'Configured destination identifier targeted by this attempt',
    destination_title VARCHAR(255) NULL COMMENT 'Destination title snapshot stored for audit and troubleshooting',
    dispatch_id BINARY(16) NULL COMMENT 'Optional child Call Dispatch execution identifier created for this alert attempt',
    phone_number VARCHAR(40) NOT NULL COMMENT 'Dialed phone number snapshot stored for the attempt',
    priority INT(11) NOT NULL DEFAULT 1 COMMENT 'Priority snapshot of the destination at the time of the attempt',
    attempt_number INT(11) NOT NULL DEFAULT 1 COMMENT 'Sequential attempt number inside the same alert execution',
    status VARCHAR(40) NOT NULL COMMENT 'Current lifecycle status of the attempt',
    started_at_utc DATETIME(6) NOT NULL COMMENT 'UTC time when the attempt started',
    finished_at_utc DATETIME(6) NULL COMMENT 'UTC time when the attempt reached a terminal state',
    error_code VARCHAR(16) NULL COMMENT 'Stable SGZ localization code for the attempt error',
    error VARCHAR(500) NULL COMMENT 'English technical fallback stored for the attempt error',
    `update` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'Last update timestamp for the attempt row',
    PRIMARY KEY (id),
    KEY ix_gatw_zabbix_attempts_alertid_attemptnumber_started (alertid, attempt_number, started_at_utc),
    CONSTRAINT fk_gatw_zabbix_attempts_alertid FOREIGN KEY (alertid)
        REFERENCES gatw_zabbix_executions (id)
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='Operational attempt records for each alert destination dial try';
