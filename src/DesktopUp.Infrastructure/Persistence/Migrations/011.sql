-- Registration only. The single scroll axis and the optional heat-driven size switch are
-- carried inside content_layout_json and validated on read by ContentLayoutPolicy, so no
-- column is added and no existing document is rewritten by this migration.
SELECT 1;