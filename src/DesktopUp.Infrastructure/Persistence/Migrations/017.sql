CREATE TABLE content_transfer_closures(operation_id TEXT NOT NULL,item_index INTEGER NOT NULL,
    body_json TEXT NOT NULL CHECK(json_valid(body_json)), PRIMARY KEY(operation_id,item_index),
    FOREIGN KEY(operation_id,item_index) REFERENCES content_transfer_items(operation_id,item_index)) STRICT;
CREATE TABLE storage_provision_closures(collection_id TEXT PRIMARY KEY,
    body_json TEXT NOT NULL CHECK(json_valid(body_json))) STRICT;