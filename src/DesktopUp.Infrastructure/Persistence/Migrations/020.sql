CREATE TABLE managed_content_observations(
    membership_id TEXT PRIMARY KEY REFERENCES content_memberships(membership_id) ON DELETE CASCADE,
    locator TEXT NOT NULL, object_identity TEXT NOT NULL) STRICT;