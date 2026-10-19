CREATE TABLE managed_storage_roots(root_id TEXT PRIMARY KEY, body_json TEXT NOT NULL CHECK(json_valid(body_json))) STRICT;
CREATE TABLE managed_storage_collections(collection_id TEXT PRIMARY KEY REFERENCES content_collections(collection_id),
    root_id TEXT NOT NULL REFERENCES managed_storage_roots(root_id), object_identity TEXT NOT NULL) STRICT;
CREATE TABLE content_transfers(operation_id TEXT PRIMARY KEY, fingerprint TEXT NOT NULL,
    body_json TEXT NOT NULL CHECK(json_valid(body_json)), reads_json TEXT NOT NULL CHECK(json_valid(reads_json))) STRICT;
CREATE TABLE content_transfer_items(operation_id TEXT NOT NULL REFERENCES content_transfers(operation_id),
    item_index INTEGER NOT NULL, phase INTEGER NOT NULL CHECK(phase BETWEEN 0 AND 7),
    receipt_json TEXT NULL CHECK(receipt_json IS NULL OR json_valid(receipt_json)), error TEXT NULL,
    PRIMARY KEY(operation_id,item_index)) STRICT;