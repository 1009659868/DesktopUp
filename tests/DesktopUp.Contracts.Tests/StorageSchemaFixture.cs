namespace DesktopUp.Contracts.Tests;

internal static class StorageSchemaFixture
{
    // Only isolated fixtures call this when constructing a genuinely older schema.
    internal const string Drop21 = "DROP TABLE native_shell_exits; DELETE FROM schema_migrations WHERE version=21; ";
    internal const string Drop20 = Drop21 + "DROP TABLE managed_content_observations; DELETE FROM schema_migrations WHERE version=20; ";
    internal const string Drop19 = Drop20 + "DROP TRIGGER IF EXISTS storage_roots_insert; DROP TRIGGER IF EXISTS storage_roots_update; DROP TRIGGER IF EXISTS storage_roots_delete; DROP TRIGGER IF EXISTS storage_collections_insert; DROP TRIGGER IF EXISTS storage_collections_update; DROP TRIGGER IF EXISTS storage_collections_delete; DROP TABLE storage_relocation_commits; DROP TABLE storage_relocations; DROP TABLE storage_location_state; ";
}
