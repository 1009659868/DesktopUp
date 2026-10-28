namespace DesktopUp.Contracts.Tests;

internal static class StorageSchemaFixture
{
    // Only isolated fixtures call this when constructing a genuinely older schema.
    internal const string Drop27 = "DROP TRIGGER content_activity_fact_added; DROP TRIGGER content_activity_item_added; DROP TABLE content_activity_recent; DROP TABLE content_activity_pending; DROP TABLE content_activity_items; DROP TABLE content_activity_state; DELETE FROM schema_migrations WHERE version=27; ";
    internal const string Drop25 = Drop27 + "DROP TABLE content_returns; DROP TABLE content_return_origins; DELETE FROM schema_migrations WHERE version=25; ";
    internal const string Drop24 = Drop25 + "DROP TABLE mapped_directory_ingress; DELETE FROM schema_migrations WHERE version=24; ";
    internal const string Drop23 = Drop24 + "DROP TABLE mapped_directory_observations; DELETE FROM schema_migrations WHERE version=23; ";
    internal const string Drop22 = Drop23 + "DROP TABLE content_renames; DELETE FROM schema_migrations WHERE version=22; ";
    internal const string Drop21 = Drop22 + "DROP TABLE native_shell_exits; DELETE FROM schema_migrations WHERE version=21; ";
    internal const string Drop20 = Drop21 + "DROP TABLE managed_content_observations; DELETE FROM schema_migrations WHERE version=20; ";
    internal const string Drop19 = Drop20 + "DROP TRIGGER IF EXISTS storage_roots_insert; DROP TRIGGER IF EXISTS storage_roots_update; DROP TRIGGER IF EXISTS storage_roots_delete; DROP TRIGGER IF EXISTS storage_collections_insert; DROP TRIGGER IF EXISTS storage_collections_update; DROP TRIGGER IF EXISTS storage_collections_delete; DROP TABLE storage_relocation_commits; DROP TABLE storage_relocations; DROP TABLE storage_location_state; ";
}
