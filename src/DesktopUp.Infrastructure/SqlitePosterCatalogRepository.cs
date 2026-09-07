using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

public sealed class SqlitePosterCatalogRepository(SqliteStateStore store) : IPosterCatalogRepository
{
    public PosterCatalogSnapshot List() => store.ExecuteRead(ReadCatalog);

    public PosterCatalogWriteResult Create(
        long expectedRevision, string operationId, PosterDefinition configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        // Caller identity/order are not authoritative. Normalize them for validation
        // and the replay fingerprint, then allocate real identities in the transaction.
        PosterDefinition input = configuration with
        {
            PosterId = "new", ThemeInstanceId = "new", StackOrder = 0
        };
        string? error = AuthoritativeStateValidationPolicy.Validate(input);
        if (error is not null)
            return Failure(PosterCatalogWriteStatus.ValidationFailed, operationId, expectedRevision, error);
        return Execute(expectedRevision, operationId, Fingerprint(new { kind = "create", input }),
            (connection, transaction, revision) =>
            {
                PosterDefinition created = WithNewIdentity(input, NextStackOrder(connection, transaction));
                SqlitePosterRepository.InsertPoster(connection, transaction, created, revision);
                return created.PosterId;
            });
    }

    public PosterCatalogWriteResult CloneConfiguration(
        long expectedRevision, string operationId, string sourcePosterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePosterId);
        return Execute(expectedRevision, operationId, Fingerprint(new { kind = "clone", sourcePosterId }),
            (connection, transaction, revision) =>
            {
                PosterDefinition source = SqlitePosterRepository.ReadPoster(connection, transaction, sourcePosterId)?.Poster
                    ?? throw new KeyNotFoundException("The source poster no longer exists.");
                PosterDefinition clone = WithNewIdentity(source, NextStackOrder(connection, transaction));
                SqlitePosterRepository.InsertPoster(connection, transaction, clone, revision);
                return clone.PosterId;
            });
    }

    public PosterCatalogWriteResult Remove(long expectedRevision, string operationId, string posterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(posterId);
        return Execute(expectedRevision, operationId, Fingerprint(new { kind = "remove", posterId }),
            (connection, transaction, revision) =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM posters WHERE poster_id = $id;";
                command.Parameters.AddWithValue("$id", posterId);
                if (command.ExecuteNonQuery() != 1)
                    throw new KeyNotFoundException("The poster no longer exists.");
                TouchProfile(connection, transaction, revision, true);
                return posterId;
            });
    }

    public PosterCatalogWriteResult InitializeEmptyProfile(long expectedRevision, string operationId) =>
        Execute(expectedRevision, operationId, Fingerprint(new { kind = "initialize-empty" }),
            (connection, transaction, revision) =>
            {
                if (ReadProfile(connection, transaction).Initialized)
                    throw new CatalogRequestException("The profile is already initialized.");
                TouchProfile(connection, transaction, revision, false);
                return null;
            });

    private PosterCatalogWriteResult Execute(
        long expectedRevision,
        string operationId,
        string fingerprint,
        Func<SqliteConnection, SqliteTransaction, long, string?> mutation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        try
        {
            StateCompareAndSwapResult result = store.ExecuteCompareAndSwap(
                expectedRevision, operationId,
                (connection, transaction, revision) =>
                {
                    string? posterId = mutation(connection, transaction, revision);
                    using SqliteCommand receipt = connection.CreateCommand();
                    receipt.Transaction = transaction;
                    receipt.CommandText = """
                        INSERT INTO poster_catalog_operations(operation_id, request_sha256, result_poster_id)
                        VALUES ($operation, $hash, $poster);
                        """;
                    receipt.Parameters.AddWithValue("$operation", operationId);
                    receipt.Parameters.AddWithValue("$hash", fingerprint);
                    receipt.Parameters.AddWithValue("$poster", (object?)posterId ?? DBNull.Value);
                    receipt.ExecuteNonQuery();
                });

            // Receipt and latest catalog are observed together. CommittedRevision is
            // the original receipt's revision; Snapshot may be newer after another writer.
            return store.ExecuteRead((connection, transaction) =>
            {
                PosterCatalogSnapshot snapshot = ReadCatalog(connection, transaction);
                if (result.Status == StateCompareAndSwapStatus.Conflict)
                    return new PosterCatalogWriteResult(PosterCatalogWriteStatus.Conflict, operationId,
                        expectedRevision, snapshot.Revision, null, null, snapshot, "The global revision changed.");

                using SqliteCommand receipt = connection.CreateCommand();
                receipt.Transaction = transaction;
                receipt.CommandText = """
                    SELECT request_sha256, result_poster_id FROM poster_catalog_operations
                    WHERE operation_id = $operation;
                    """;
                receipt.Parameters.AddWithValue("$operation", operationId);
                using SqliteDataReader reader = receipt.ExecuteReader();
                if (!reader.Read() || reader.GetString(0) != fingerprint)
                    return new PosterCatalogWriteResult(PosterCatalogWriteStatus.ValidationFailed, operationId,
                        expectedRevision, snapshot.Revision, null, null, snapshot,
                        "The operation ID belongs to a different request; use a new ID.");
                string? posterId = reader.IsDBNull(1) ? null : reader.GetString(1);
                return new PosterCatalogWriteResult(
                    result.Status == StateCompareAndSwapStatus.Replayed
                        ? PosterCatalogWriteStatus.Replayed : PosterCatalogWriteStatus.Committed,
                    operationId, expectedRevision, snapshot.Revision,
                    result.CommittedRevision, posterId, snapshot, null);
            });
        }
        catch (KeyNotFoundException exception)
        {
            return Failure(PosterCatalogWriteStatus.NotFound, operationId, expectedRevision, exception.Message);
        }
        catch (CatalogRequestException exception)
        {
            return Failure(PosterCatalogWriteStatus.ValidationFailed, operationId, expectedRevision, exception.Message);
        }
        catch (Exception exception)
        {
            return Failure(PosterCatalogWriteStatus.Failed, operationId, expectedRevision, exception.Message);
        }
    }

    private PosterCatalogWriteResult Failure(
        PosterCatalogWriteStatus status, string operationId, long expectedRevision, string error)
    {
        PosterCatalogSnapshot? snapshot;
        try { snapshot = List(); }
        catch { snapshot = null; }
        return new PosterCatalogWriteResult(status, operationId, expectedRevision,
            snapshot?.Revision ?? -1, null, null, snapshot, error);
    }

    private static PosterDefinition WithNewIdentity(PosterDefinition source, long stackOrder) => source with
    {
        PosterId = $"poster-{Guid.NewGuid():N}",
        ThemeInstanceId = $"theme-{Guid.NewGuid():N}",
        StackOrder = stackOrder
    };

    private static string Fingerprint<T>(T request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));

    private static long NextStackOrder(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT MAX(stack_order) FROM posters;";
        object? value = command.ExecuteScalar();
        return value is null or DBNull ? 0 : checked(Convert.ToInt64(value) + 1);
    }

    internal static (bool Initialized, long Version) ReadProfile(
        SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT profile_initialized, catalog_version FROM poster_profile WHERE singleton_id = 1;
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read()) throw new InvalidDataException("The poster profile is missing.");
        return (reader.GetInt64(0) != 0, reader.GetInt64(1));
    }

    internal static void TouchProfile(
        SqliteConnection connection, SqliteTransaction transaction, long revision, bool membershipOrOrderChanged)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE poster_profile SET profile_initialized = 1,
                catalog_version = catalog_version + $increment, state_revision = $revision
            WHERE singleton_id = 1;
            """;
        command.Parameters.AddWithValue("$increment", membershipOrOrderChanged ? 1 : 0);
        command.Parameters.AddWithValue("$revision", revision);
        if (command.ExecuteNonQuery() != 1) throw new InvalidDataException("The poster profile is missing.");
    }

    internal static PosterCatalogSnapshot ReadCatalog(SqliteConnection connection, SqliteTransaction transaction)
    {
        var profile = ReadProfile(connection, transaction);
        long revision = SqlitePosterRepository.ReadGlobalRevision(connection, transaction);
        var ids = new List<string>();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT poster_id FROM posters ORDER BY stack_order, poster_id;";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read()) ids.Add(reader.GetString(0));
        }
        var posters = new List<PosterStateSnapshot>(ids.Count);
        foreach (string id in ids)
            posters.Add(SqlitePosterRepository.ReadPoster(connection, transaction, id)
                ?? throw new InvalidDataException("A catalog entry is missing."));
        return new PosterCatalogSnapshot(revision, profile.Version, profile.Initialized, posters.AsReadOnly());
    }

    private sealed class CatalogRequestException(string message) : Exception(message);
}
