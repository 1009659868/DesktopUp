CREATE TABLE mapped_directory_observations(
    collection_id TEXT PRIMARY KEY REFERENCES content_collections(collection_id) ON DELETE CASCADE,
    root_identity TEXT NOT NULL, observations_json TEXT NOT NULL CHECK(json_valid(observations_json))) STRICT;