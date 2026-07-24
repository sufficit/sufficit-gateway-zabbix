-- Adds protected customer-Zabbix automation settings to the existing integration row.
-- Apply to the same MySQL/MariaDB database that owns gatw_zabbix_integrations.

ALTER TABLE `gatw_zabbix_integrations`
    ADD COLUMN IF NOT EXISTS `zabbix_api_url` VARCHAR(2048) NULL AFTER `call_dispatch_id`,
    ADD COLUMN IF NOT EXISTS `zabbix_api_token_protected` TEXT NULL AFTER `zabbix_api_url`,
    ADD COLUMN IF NOT EXISTS `zabbix_minimum_severity` INT(1) NOT NULL DEFAULT 3 AFTER `zabbix_api_token_protected`,
    ADD COLUMN IF NOT EXISTS `zabbix_media_type_id` VARCHAR(32) NULL AFTER `zabbix_minimum_severity`,
    ADD COLUMN IF NOT EXISTS `zabbix_action_id` VARCHAR(32) NULL AFTER `zabbix_media_type_id`,
    ADD COLUMN IF NOT EXISTS `zabbix_user_id` VARCHAR(32) NULL AFTER `zabbix_action_id`,
    ADD COLUMN IF NOT EXISTS `zabbix_version` VARCHAR(32) NULL AFTER `zabbix_user_id`,
    ADD COLUMN IF NOT EXISTS `zabbix_last_configured_at_utc` DATETIME(6) NULL AFTER `zabbix_version`;
