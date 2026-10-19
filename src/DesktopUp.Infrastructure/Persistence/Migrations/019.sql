CREATE TABLE storage_location_state(singleton_id INTEGER PRIMARY KEY CHECK(singleton_id=1),
    format_version INTEGER NOT NULL CHECK(format_version>=1), profile_identity TEXT NULL,
    version INTEGER NOT NULL CHECK(version>=0), root_set_version INTEGER NOT NULL CHECK(root_set_version>=0),
    active_root_id TEXT NULL REFERENCES managed_storage_roots(root_id)) STRICT;
INSERT INTO storage_location_state VALUES(1,1,NULL,0,0,NULL);
CREATE TABLE storage_relocations(operation_id TEXT PRIMARY KEY, fingerprint TEXT NOT NULL,
    body_json TEXT NOT NULL CHECK(json_valid(body_json)), phase INTEGER NOT NULL CHECK(phase BETWEEN 1 AND 6),
    targets_json TEXT NULL CHECK(targets_json IS NULL OR json_valid(targets_json)), closure TEXT NULL) STRICT;
CREATE TABLE storage_relocation_commits(operation_id TEXT PRIMARY KEY REFERENCES storage_relocations(operation_id),
    fingerprint TEXT NOT NULL, active_root_id TEXT NOT NULL, revision INTEGER NOT NULL CHECK(revision>0)) STRICT;
CREATE TRIGGER storage_roots_insert AFTER INSERT ON managed_storage_roots BEGIN UPDATE storage_location_state SET root_set_version=root_set_version+1; END;
CREATE TRIGGER storage_roots_update AFTER UPDATE ON managed_storage_roots BEGIN UPDATE storage_location_state SET root_set_version=root_set_version+1; END;
CREATE TRIGGER storage_roots_delete AFTER DELETE ON managed_storage_roots BEGIN UPDATE storage_location_state SET root_set_version=root_set_version+1; END;
CREATE TRIGGER storage_collections_insert AFTER INSERT ON managed_storage_collections BEGIN UPDATE storage_location_state SET root_set_version=root_set_version+1; END;
CREATE TRIGGER storage_collections_update AFTER UPDATE ON managed_storage_collections BEGIN UPDATE storage_location_state SET root_set_version=root_set_version+1; END;
CREATE TRIGGER storage_collections_delete AFTER DELETE ON managed_storage_collections BEGIN UPDATE storage_location_state SET root_set_version=root_set_version+1; END;