CREATE TABLE native_shell_exits(operation_id TEXT PRIMARY KEY, body_json TEXT NOT NULL,
    released INTEGER, closed INTEGER NOT NULL DEFAULT 0 CHECK(closed IN(0,1))) STRICT;