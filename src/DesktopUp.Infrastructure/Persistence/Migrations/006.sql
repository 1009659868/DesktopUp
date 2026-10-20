CREATE TABLE spatial_continuity (
    workspace_id TEXT PRIMARY KEY NOT NULL,
    schema_version INTEGER NOT NULL CHECK (schema_version = 1),
    body_json TEXT NOT NULL CHECK (json_valid(body_json)),
    committed_revision INTEGER NOT NULL REFERENCES state_operations(committed_revision) DEFERRABLE INITIALLY DEFERRED
) STRICT;