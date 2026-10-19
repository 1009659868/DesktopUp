CREATE TABLE content_head (
    singleton_id INTEGER PRIMARY KEY CHECK (singleton_id = 1),
    version INTEGER NOT NULL CHECK (version >= 0)
) STRICT;
INSERT INTO content_head VALUES (1, 0);
CREATE TABLE content_items (
    item_id TEXT PRIMARY KEY NOT NULL,
    body_json TEXT NOT NULL CHECK (json_valid(body_json) AND json_extract(body_json, '$.id') = item_id)
) STRICT;
CREATE TABLE content_collections (
    collection_id TEXT PRIMARY KEY NOT NULL,
    body_json TEXT NOT NULL CHECK (json_valid(body_json) AND json_extract(body_json, '$.id') = collection_id)
) STRICT;
CREATE TABLE content_memberships (
    membership_id TEXT PRIMARY KEY NOT NULL,
    collection_id TEXT NOT NULL REFERENCES content_collections(collection_id),
    item_id TEXT NOT NULL REFERENCES content_items(item_id),
    body_json TEXT NOT NULL CHECK (json_valid(body_json)
        AND json_extract(body_json, '$.id') = membership_id
        AND json_extract(body_json, '$.collectionId') = collection_id
        AND json_extract(body_json, '$.contentItemId') = item_id),
    UNIQUE(collection_id, item_id)
) STRICT;
CREATE TABLE content_poster_bindings (
    poster_id TEXT PRIMARY KEY NOT NULL REFERENCES posters(poster_id) ON DELETE CASCADE,
    collection_id TEXT NOT NULL REFERENCES content_collections(collection_id),
    body_json TEXT NOT NULL CHECK (json_valid(body_json)
        AND json_extract(body_json, '$.posterId') = poster_id
        AND json_extract(body_json, '$.collectionId') = collection_id)
) STRICT;
CREATE TABLE content_poster_items (
    relation_id TEXT PRIMARY KEY NOT NULL,
    poster_id TEXT NOT NULL REFERENCES content_poster_bindings(poster_id) ON DELETE CASCADE,
    membership_id TEXT NOT NULL REFERENCES content_memberships(membership_id),
    body_json TEXT NOT NULL CHECK (json_valid(body_json)
        AND json_extract(body_json, '$.id') = relation_id
        AND json_extract(body_json, '$.posterId') = poster_id
        AND json_extract(body_json, '$.membershipId') = membership_id),
    UNIQUE(poster_id, membership_id)
) STRICT;
CREATE TABLE content_demo_archive (
    poster_id TEXT PRIMARY KEY NOT NULL REFERENCES posters(poster_id) ON DELETE CASCADE,
    original_layout_json TEXT NULL,
    archived_revision INTEGER NOT NULL CHECK (archived_revision >= 1)
) STRICT;