CREATE TABLE managed_storage_adoptions(
    fingerprint TEXT PRIMARY KEY,
    collection_id TEXT NOT NULL REFERENCES managed_storage_collections(collection_id),
    body_json TEXT NOT NULL CHECK(json_valid(body_json)),
    activated INTEGER NOT NULL CHECK(activated IN (0,1))) STRICT;
CREATE UNIQUE INDEX one_active_storage_adoption ON managed_storage_adoptions(collection_id) WHERE activated=1;