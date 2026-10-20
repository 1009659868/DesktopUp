ALTER TABLE app_settings ADD COLUMN entity_version INTEGER NOT NULL DEFAULT 1 CHECK (entity_version >= 1);
CREATE TABLE state_command_receipts (
    request_id TEXT PRIMARY KEY NOT NULL,
    request_sha256 TEXT NOT NULL,
    outcome TEXT NOT NULL CHECK (outcome IN ('committed', 'no_change')),
    observed_revision INTEGER NOT NULL CHECK (observed_revision >= 0),
    committed_revision INTEGER NULL,
    result_poster_id TEXT NULL,
    CHECK ((outcome = 'committed' AND committed_revision IS NOT NULL) OR
        (outcome = 'no_change' AND committed_revision IS NULL)),
    FOREIGN KEY (committed_revision) REFERENCES state_operations(committed_revision)
        DEFERRABLE INITIALLY DEFERRED
) STRICT;