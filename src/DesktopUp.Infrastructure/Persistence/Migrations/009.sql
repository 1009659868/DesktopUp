ALTER TABLE posters ADD COLUMN placement_pending INTEGER NOT NULL DEFAULT 0
    CHECK (placement_pending IN (0, 1));