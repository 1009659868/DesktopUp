CREATE TABLE content_activity_items (
    content_item_id TEXT PRIMARY KEY NOT NULL,
    added_at_unix_ms INTEGER NOT NULL,
    total_launches INTEGER NOT NULL DEFAULT 0 CHECK(total_launches >= 0),
    last_launched_unix_ms INTEGER NULL,
    CHECK((total_launches = 0 AND last_launched_unix_ms IS NULL) OR
          (total_launches > 0 AND last_launched_unix_ms IS NOT NULL))
) STRICT;
INSERT INTO content_activity_items(content_item_id, added_at_unix_ms)
    SELECT item_id, CAST(unixepoch('subsec') * 1000 AS INTEGER) FROM content_items;
CREATE TRIGGER content_activity_item_added AFTER INSERT ON content_items BEGIN
    INSERT INTO content_activity_items(content_item_id, added_at_unix_ms)
        VALUES(NEW.item_id, CAST(unixepoch('subsec') * 1000 AS INTEGER))
        ON CONFLICT(content_item_id) DO NOTHING;
END;
CREATE TABLE content_activity_pending (
    sequence INTEGER PRIMARY KEY AUTOINCREMENT,
    activation_id TEXT UNIQUE NOT NULL REFERENCES content_open_facts(activation_id)
) STRICT;
INSERT INTO content_activity_pending(activation_id)
    SELECT activation_id FROM content_open_facts ORDER BY activation_id;
CREATE TRIGGER content_activity_fact_added AFTER INSERT ON content_open_facts BEGIN
    INSERT INTO content_activity_pending(activation_id) VALUES(NEW.activation_id);
END;
CREATE TABLE content_activity_state (
    singleton_id INTEGER PRIMARY KEY CHECK(singleton_id = 1),
    consumed_position INTEGER NOT NULL CHECK(consumed_position >= 0)
) STRICT;
INSERT INTO content_activity_state VALUES(1, 0);
CREATE TABLE content_activity_recent (
    content_item_id TEXT NOT NULL,
    sequence INTEGER PRIMARY KEY,
    activation_id TEXT UNIQUE NOT NULL,
    observed_unix_ms INTEGER NOT NULL,
    poster_id TEXT NOT NULL
) STRICT;
CREATE INDEX content_activity_recent_by_item
    ON content_activity_recent(content_item_id, observed_unix_ms DESC, sequence DESC);