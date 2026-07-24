-- 2026-04-10
-- Align the persisted Zabbix gateway schema with the Call Dispatch integration rollout.
-- This keeps the gateway able to store the selected preset on integrations/executions
-- and the spawned child dispatch correlation on each attempt.

ALTER TABLE `gatw_zabbix_integrations`
    ADD COLUMN IF NOT EXISTS `call_dispatch_id` BINARY(16) NULL AFTER `digit`;

ALTER TABLE `gatw_zabbix_executions`
    ADD COLUMN IF NOT EXISTS `call_dispatch_id` BINARY(16) NULL AFTER `digit`;

ALTER TABLE `gatw_zabbix_attempts`
    ADD COLUMN IF NOT EXISTS `dispatch_id` BINARY(16) NULL AFTER `destination_title`;

ALTER TABLE `gatw_zabbix_integrations`
    MODIFY COLUMN `call_dispatch_id` BINARY(16) NULL COMMENT 'Optional Call Dispatch preset used to start outbound alert calls; null keeps validation and persistence without telephony kickoff';

ALTER TABLE `gatw_zabbix_executions`
    MODIFY COLUMN `call_dispatch_id` BINARY(16) NULL COMMENT 'Optional Call Dispatch preset selected for this alert execution; null keeps the flow in validation-only mode';

ALTER TABLE `gatw_zabbix_attempts`
    MODIFY COLUMN `dispatch_id` BINARY(16) NULL COMMENT 'Optional child Call Dispatch execution identifier created for this alert attempt';
