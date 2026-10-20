CREATE TABLE content_renames(operation_id TEXT PRIMARY KEY,body_json TEXT NOT NULL,
    closed INTEGER NOT NULL DEFAULT 0 CHECK(closed IN(0,1))) STRICT;