ALTER TABLE posters ADD COLUMN purpose TEXT NOT NULL DEFAULT 'general';
ALTER TABLE posters ADD COLUMN is_visible INTEGER NOT NULL DEFAULT 1 CHECK (is_visible IN (0, 1));
ALTER TABLE posters ADD COLUMN stack_order INTEGER NOT NULL DEFAULT 0 CHECK (stack_order >= 0);
ALTER TABLE posters ADD COLUMN entity_version INTEGER NOT NULL DEFAULT 1 CHECK (entity_version >= 1);
ALTER TABLE posters ADD COLUMN collection_id TEXT NULL;
ALTER TABLE posters ADD COLUMN theme_instance_id TEXT NOT NULL DEFAULT '';
UPDATE posters SET theme_instance_id = 'theme-' || poster_id;
UPDATE posters SET stack_order = (
    SELECT COUNT(*) FROM posters p WHERE p.poster_id < posters.poster_id);
CREATE UNIQUE INDEX posters_theme_instance_id ON posters(theme_instance_id);
CREATE INDEX posters_stack_order ON posters(stack_order, poster_id);

CREATE TABLE poster_profile (
    singleton_id INTEGER PRIMARY KEY NOT NULL CHECK (singleton_id = 1),
    profile_initialized INTEGER NOT NULL CHECK (profile_initialized IN (0, 1)),
    catalog_version INTEGER NOT NULL CHECK (catalog_version >= 0),
    state_revision INTEGER NOT NULL CHECK (state_revision >= 0)
) STRICT;
INSERT INTO poster_profile
SELECT 1, EXISTS(SELECT 1 FROM posters),
    CASE WHEN EXISTS(SELECT 1 FROM posters) THEN 1 ELSE 0 END, revision
FROM state_head WHERE singleton_id = 1;

CREATE TABLE poster_catalog_operations (
    operation_id TEXT PRIMARY KEY NOT NULL,
    request_sha256 TEXT NOT NULL,
    result_poster_id TEXT NULL,
    FOREIGN KEY (operation_id) REFERENCES state_operations(operation_id)
        DEFERRABLE INITIALLY DEFERRED
) STRICT;