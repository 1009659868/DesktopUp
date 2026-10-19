CREATE TABLE schema_migrations (
    version INTEGER PRIMARY KEY NOT NULL,
    name TEXT NOT NULL,
    checksum_sha256 TEXT NOT NULL,
    applied_utc TEXT NOT NULL
) STRICT;

CREATE TABLE state_head (
    singleton_id INTEGER PRIMARY KEY NOT NULL CHECK (singleton_id = 1),
    schema_version INTEGER NOT NULL,
    revision INTEGER NOT NULL CHECK (revision >= 0),
    last_operation_id TEXT NULL,
    updated_utc TEXT NOT NULL
) STRICT;

CREATE TABLE state_operations (
    operation_id TEXT PRIMARY KEY NOT NULL,
    expected_revision INTEGER NOT NULL CHECK (expected_revision >= 0),
    committed_revision INTEGER NOT NULL UNIQUE,
    committed_utc TEXT NOT NULL,
    CHECK (committed_revision = expected_revision + 1)
) STRICT;