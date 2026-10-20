ALTER TABLE posters ADD COLUMN auto_collapse_delay_seconds INTEGER NOT NULL DEFAULT 3
    CHECK (auto_collapse_delay_seconds BETWEEN 1 AND 300);