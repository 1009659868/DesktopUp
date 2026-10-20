CREATE TABLE content_open_requests (
    activation_id TEXT PRIMARY KEY NOT NULL,
    request_json TEXT NOT NULL CHECK(json_valid(request_json)),
    target_hash TEXT NOT NULL,
    completed INTEGER NOT NULL CHECK(completed IN(0,1)),
    result_json TEXT NULL CHECK(result_json IS NULL OR json_valid(result_json))
) STRICT;
CREATE INDEX content_open_pending ON content_open_requests(target_hash, completed);
CREATE TABLE content_open_facts (
    activation_id TEXT PRIMARY KEY NOT NULL REFERENCES content_open_requests(activation_id),
    fact_json TEXT NOT NULL CHECK(json_valid(fact_json))
) STRICT;