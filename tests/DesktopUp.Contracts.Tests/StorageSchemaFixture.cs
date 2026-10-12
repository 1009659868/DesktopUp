namespace DesktopUp.Contracts.Tests;

internal static class StorageSchemaFixture
{
    // Only isolated fixtures call this when constructing a genuinely older schema.
    internal const string Drop19 = "DROP TRIGGER IF EXISTS storage_roots_insert; DROP TRIGGER IF EXISTS storage_roots_update; DROP TRIGGER IF EXISTS storage_roots_delete; DROP TRIGGER IF EXISTS storage_collections_insert; DROP TRIGGER IF EXISTS storage_collections_update; DROP TRIGGER IF EXISTS storage_collections_delete; DROP TABLE storage_relocation_commits; DROP TABLE storage_relocations; DROP TABLE storage_location_state; ";
}
