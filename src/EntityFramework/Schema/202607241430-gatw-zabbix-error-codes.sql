ALTER TABLE `gatw_zabbix_executions`
    ADD COLUMN IF NOT EXISTS `error_code` VARCHAR(16) NULL AFTER `finished_at_utc`;

ALTER TABLE `gatw_zabbix_attempts`
    ADD COLUMN IF NOT EXISTS `error_code` VARCHAR(16) NULL AFTER `finished_at_utc`;
