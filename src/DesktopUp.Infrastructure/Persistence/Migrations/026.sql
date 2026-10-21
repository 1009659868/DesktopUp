-- Existing Initialize requires a verified pre-migration backup; this script and its
-- ledger entry share one transaction. Replace dimension columns, not the parent
-- table: poster identity, indexes and ON DELETE CASCADE children remain untouched.
ALTER TABLE posters ADD COLUMN next_width_dip REAL NOT NULL DEFAULT 96.0 CHECK (next_width_dip >= 96.0);
-- statement-break
ALTER TABLE posters ADD COLUMN next_height_dip REAL NOT NULL DEFAULT 96.0 CHECK (next_height_dip >= 96.0);
-- statement-break
UPDATE posters SET next_width_dip = width_dip, next_height_dip = expanded_height_dip;
-- statement-break
ALTER TABLE posters DROP COLUMN width_dip;
-- statement-break
ALTER TABLE posters DROP COLUMN expanded_height_dip;
-- statement-break
ALTER TABLE posters RENAME COLUMN next_width_dip TO width_dip;
-- statement-break
ALTER TABLE posters RENAME COLUMN next_height_dip TO expanded_height_dip;