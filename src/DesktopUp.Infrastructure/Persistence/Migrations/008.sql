-- BUG-005: only the exact shipped basic-box payload AND a committed UI catalog receipt.
-- Never interpret arbitrary grid/custom/future/edited JSON as this historical mistake.
CREATE TEMP TABLE canonical_layout_repair_ids (poster_id TEXT PRIMARY KEY NOT NULL);
INSERT INTO canonical_layout_repair_ids
SELECT p.poster_id FROM posters p
WHERE p.layout_kind = 'grid' AND p.template_key = 'basic-box'
  AND p.layout_parameters_json = '{"schemaVersion":1,"content":{"itemWidthDip":100,"itemHeightDip":72,"iconSizeDip":40,"gapDip":12,"labelFontSizeDip":12,"titleFontSizeDip":24},"theme":{"background":"#282B30","foreground":"#F2F2F2","accent":"#68BDFF"}}'
  AND EXISTS (SELECT 1 FROM state_command_receipts r
      WHERE r.result_poster_id = p.poster_id AND r.outcome = 'committed'
        AND (r.request_id GLOB 'controlcenter-catalog-*' OR r.request_id GLOB 'tray-create-*'));
INSERT INTO state_operations (operation_id,expected_revision,committed_revision,committed_utc)
SELECT 'migration-v8-canonical-created-layout',revision,revision+1,strftime('%Y-%m-%dT%H:%M:%fZ','now')
FROM state_head WHERE singleton_id=1 AND EXISTS(SELECT 1 FROM canonical_layout_repair_ids);
UPDATE posters SET layout_kind='adaptive-grid',entity_version=entity_version+1,
    state_revision=(SELECT revision+1 FROM state_head WHERE singleton_id=1),
    updated_utc=strftime('%Y-%m-%dT%H:%M:%fZ','now')
WHERE poster_id IN (SELECT poster_id FROM canonical_layout_repair_ids);
UPDATE state_head SET revision=revision+1,last_operation_id='migration-v8-canonical-created-layout'
WHERE singleton_id=1 AND EXISTS(SELECT 1 FROM canonical_layout_repair_ids);
DROP TABLE canonical_layout_repair_ids;