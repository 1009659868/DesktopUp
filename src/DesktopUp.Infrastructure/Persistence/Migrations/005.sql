ALTER TABLE posters ADD COLUMN layout_generation INTEGER NOT NULL DEFAULT 0 CHECK (layout_generation >= 0);
ALTER TABLE posters ADD COLUMN last_layout_operation_id TEXT NULL;
CREATE TABLE spatial_operations (
    operation_id TEXT PRIMARY KEY NOT NULL,
    source_poster_id TEXT NOT NULL REFERENCES posters(poster_id) ON DELETE CASCADE,
    schema_version INTEGER NOT NULL CHECK (schema_version = 1),
    status TEXT NOT NULL CHECK (status IN ('active', 'consumed', 'invalidated')),
    body_json TEXT NOT NULL CHECK (json_valid(body_json)),
    committed_revision INTEGER NOT NULL REFERENCES state_operations(committed_revision) DEFERRABLE INITIALLY DEFERRED
) STRICT;
CREATE INDEX spatial_source ON spatial_operations(source_poster_id, status);
CREATE TABLE spatial_participants (
    operation_id TEXT NOT NULL REFERENCES spatial_operations(operation_id) ON DELETE CASCADE,
    poster_id TEXT NOT NULL,
    expected_layout_generation INTEGER NOT NULL CHECK (expected_layout_generation >= 1),
    proof_json TEXT NOT NULL CHECK (json_valid(proof_json)),
    invalidated_reason TEXT NULL,
    PRIMARY KEY(operation_id, poster_id)
) STRICT;