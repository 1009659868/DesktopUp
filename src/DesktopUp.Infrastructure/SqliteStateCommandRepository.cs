using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

// Every read-set check, mutation, receipt and returned snapshot shares one SQLite transaction.
public sealed class SqliteStateCommandRepository(SqliteStateStore store) : IStateCommandRepository
{
    public DesktopStateSnapshot Read() => store.ExecuteRead(ReadSnapshot);

    public DesktopStateSnapshot EnsureInitialized(PosterDefinition defaultPoster) =>
        store.ExecuteWrite((connection, transaction) =>
        {
            DesktopStateSnapshot state = ReadSnapshot(connection, transaction);
            if (state.Catalog.ProfileInitialized) return state;
            string? error = AuthoritativeStateValidationPolicy.Validate(defaultPoster);
            if (error is not null) throw new ArgumentException(error, nameof(defaultPoster));
            long revision = checked(state.Revision + 1);
            SqlitePosterRepository.InsertPoster(connection, transaction, defaultPoster, revision);
            AdvanceHead(connection, transaction, state.Revision, "initialize-" + Guid.NewGuid().ToString("N"));
            return ReadSnapshot(connection, transaction);
        });

    public StateTransactionResult Execute(StateTransactionRequest request,
        Func<DesktopStateSnapshot, StateMutationPlan> prepare) => store.ExecuteWrite((connection, transaction) =>
            ExecuteInTransaction(connection, transaction, request, prepare));

    internal static StateTransactionResult ExecuteInTransaction(SqliteConnection connection, SqliteTransaction transaction,
        StateTransactionRequest request, Func<DesktopStateSnapshot, StateMutationPlan> prepare, Guid? authorizedTransferId = null,
        Guid? authorizedExternalSyncId = null)
    {
        DesktopStateSnapshot before = ReadSnapshot(connection, transaction);
        StateTransactionResult Result(StateCommandStatus status, string? error = null,
            long? committed = null, string? id = null, IReadOnlyList<StateReadConflict>? conflicts = null) =>
            new(status, before, committed, id, conflicts ?? Array.Empty<StateReadConflict>(), error);

        using (SqliteCommand receipt = connection.CreateCommand())
        {
            receipt.Transaction = transaction;
            receipt.CommandText = "SELECT request_sha256, committed_revision, result_poster_id FROM state_command_receipts WHERE request_id = $id;";
            receipt.Parameters.AddWithValue("$id", request.RequestId);
            using SqliteDataReader reader = receipt.ExecuteReader();
            if (reader.Read())
                return reader.GetString(0) != request.Fingerprint
                    ? Result(StateCommandStatus.ValidationFailed, "Request id was already used for a different command.")
                    : Result(StateCommandStatus.Replayed, committed: reader.IsDBNull(1) ? null : reader.GetInt64(1),
                        id: reader.IsDBNull(2) ? null : reader.GetString(2));
        }
        using (SqliteCommand legacy = connection.CreateCommand())
        {
            legacy.Transaction = transaction;
            legacy.CommandText = "SELECT COUNT(*) FROM state_operations WHERE operation_id = $id;";
            legacy.Parameters.AddWithValue("$id", request.RequestId);
            if (Convert.ToInt64(legacy.ExecuteScalar()) != 0)
                return Result(StateCommandStatus.ValidationFailed, "Request id belongs to an earlier protocol.");
        }
        if (!before.Catalog.ProfileInitialized) return Result(StateCommandStatus.NotReady, "Profile is not initialized.");
        if (request.ObservedRevision > before.Revision)
            return Result(StateCommandStatus.Conflict, "Observed revision is ahead of the authoritative state.");

        IReadOnlyList<StateReadConflict> conflicts = CheckReadSet(before, request);
        if (conflicts.Count > 0)
            return Result(conflicts.Any(c => c.Reason == "NotFound") ? StateCommandStatus.NotFound : StateCommandStatus.Conflict,
                "The command read set is no longer current.", conflicts: conflicts);

        StateMutationPlan plan = prepare(before);
        if (plan.Rejection is { } rejected) return Result(rejected, plan.Error);
        if (!SqliteStorageRelocationJournal.AllowsMutation(connection, transaction, plan))
            return Result(StateCommandStatus.Conflict, "Storage relocation owns the affected content; retry after it finishes.");
        if (plan.TransferReceiptId != authorizedTransferId || plan.ExternalSyncReceiptId != authorizedExternalSyncId ||
            (authorizedTransferId is not null && authorizedExternalSyncId is not null))
            return Result(StateCommandStatus.ValidationFailed, "Only the verified transfer transaction may mutate storage membership.");
        if (authorizedExternalSyncId is null && !SqliteTransferJournal.ValidStorageRemoval(connection, transaction, plan))
            return Result(StateCommandStatus.ValidationFailed, "Missing or mismatched durable file result.");
        if (!SqliteTransferJournal.AllowsConcurrentMutation(connection, transaction, plan))
            return Result(StateCommandStatus.Conflict, "A file transfer owns the affected state; finish recovery before editing it.");
        string? coverageError = ValidatePlan(before, request.ReadSet, plan);
        if (coverageError is not null) return Result(StateCommandStatus.ValidationFailed, coverageError);
        long? committedRevision = null;
        if (plan.HasChanges)
        {
            committedRevision = checked(before.Revision + 1);
            foreach (PosterDefinition poster in plan.Updates)
                SqlitePosterRepository.UpdatePoster(connection, transaction, poster, committedRevision.Value,
                    request.RequestId, plan.TouchLayoutIds.Contains(poster.PosterId));
            foreach (PosterDefinition poster in plan.Creates)
                SqlitePosterRepository.InsertPoster(connection, transaction, poster, committedRevision.Value);
            foreach (string id in plan.Removes)
            {
                using SqliteCommand remove = connection.CreateCommand();
                remove.Transaction = transaction;
                remove.CommandText = "DELETE FROM posters WHERE poster_id = $id;";
                remove.Parameters.AddWithValue("$id", id);
                if (remove.ExecuteNonQuery() != 1) throw new InvalidDataException("Removal target disappeared.");
                SqlitePosterCatalogRepository.TouchProfile(connection, transaction, committedRevision.Value, true);
            }
            if (plan.Settings is { } settings)
                SqliteSettingsRepository.UpdateSettings(connection, transaction, settings, committedRevision.Value);
            SqliteSpatialProofRepository.Apply(connection, transaction, plan.NewSpatialProof, plan.ConsumeSpatialOperationId, committedRevision.Value);
            if (plan.NewContinuity is { } continuity)
                SqliteSpatialContinuityRepository.Write(connection, transaction, continuity, committedRevision.Value);
            SqliteContentRepository.Apply(connection, transaction, before, plan, committedRevision.Value);
            AdvanceHead(connection, transaction, before.Revision, request.RequestId);
        }
        using (SqliteCommand receipt = connection.CreateCommand())
        {
            receipt.Transaction = transaction;
            receipt.CommandText = """
                INSERT INTO state_command_receipts
                    (request_id, request_sha256, outcome, observed_revision, committed_revision, result_poster_id)
                VALUES ($id, $hash, $outcome, $observed, $committed, $poster);
                """;
            receipt.Parameters.AddWithValue("$id", request.RequestId);
            receipt.Parameters.AddWithValue("$hash", request.Fingerprint);
            receipt.Parameters.AddWithValue("$outcome", plan.HasChanges ? "committed" : "no_change");
            receipt.Parameters.AddWithValue("$observed", request.ObservedRevision);
            receipt.Parameters.AddWithValue("$committed", (object?)committedRevision ?? DBNull.Value);
            receipt.Parameters.AddWithValue("$poster", (object?)plan.ResultPosterId ?? DBNull.Value);
            receipt.ExecuteNonQuery();
        }
        return new StateTransactionResult(plan.HasChanges ? StateCommandStatus.Committed : StateCommandStatus.NoChange,
            ReadSnapshot(connection, transaction), committedRevision, plan.ResultPosterId,
            Array.Empty<StateReadConflict>(), null);
    }

    internal static DesktopStateSnapshot ReadSnapshot(SqliteConnection connection, SqliteTransaction transaction) =>
        new(SqlitePosterCatalogRepository.ReadCatalog(connection, transaction),
            SqliteSettingsRepository.ReadSettings(connection, transaction))
            { ContentCatalog = SqliteContentRepository.Read(connection, transaction),
                ContentVersion = SqliteContentRepository.ReadVersion(connection, transaction),
                SpatialOperations = SqliteSpatialProofRepository.Read(connection, transaction),
                SpatialContinuities = SqliteSpatialContinuityRepository.Read(connection, transaction) };

    private static IReadOnlyList<StateReadConflict> CheckReadSet(DesktopStateSnapshot state, StateTransactionRequest request)
    {
        var conflicts = new List<StateReadConflict>();
        if (request.ReadSet is not { } reads)
        {
            if (request.ObservedRevision != state.Revision)
                conflicts.Add(new("global", "state", request.ObservedRevision, state.Revision, "Conflict"));
        }
        else
        {
            var posters = state.Catalog.Posters.ToDictionary(p => p.Poster.PosterId, StringComparer.Ordinal);
            foreach (var (id, expected) in reads.Posters)
            {
                if (!posters.TryGetValue(id, out PosterStateSnapshot? poster))
                    conflicts.Add(new("poster", id, expected, null, "NotFound"));
                else if (poster.EntityVersion != expected)
                    conflicts.Add(new("poster", id, expected, poster.EntityVersion, "Conflict"));
            }
            if (reads.SettingsVersion is { } settings && settings != state.Settings.EntityVersion)
                conflicts.Add(new("settings", "app", settings, state.Settings.EntityVersion, "Conflict"));
            if (reads.CatalogVersion is { } catalog && catalog != state.Catalog.CatalogVersion)
                conflicts.Add(new("catalog", "posters", catalog, state.Catalog.CatalogVersion, "Conflict"));
            if (reads.ContentVersion is { } content && content != state.ContentVersion)
                conflicts.Add(new("catalog", "content", content, state.ContentVersion, "Conflict"));
        }
        return conflicts.AsReadOnly();
    }

    private static string? ValidatePlan(DesktopStateSnapshot before, StateReadSet? reads, StateMutationPlan plan)
    {
        if (ContentReferenceIntegrity.ValidateMutation(before, reads, plan) is { } contentError) return contentError;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var existing = before.Catalog.Posters.ToDictionary(p => p.Poster.PosterId, StringComparer.Ordinal);
        foreach (PosterDefinition poster in plan.Updates.Concat(plan.Creates))
        {
            string? error = AuthoritativeStateValidationPolicy.Validate(poster);
            if (error is not null) return error;
            if (!ids.Add(poster.PosterId)) return "Duplicate write target.";
        }
        foreach (string id in plan.Removes)
            if (!ids.Add(id)) return "Duplicate write/remove target.";
        foreach (PosterDefinition poster in plan.Creates)
            if (existing.ContainsKey(poster.PosterId)) return "Create target already exists.";
        foreach (string id in plan.Updates.Select(p => p.PosterId).Concat(plan.Removes))
        {
            if (!existing.ContainsKey(id)) return "Write target does not exist.";
            if (reads is not null && !reads.Posters.ContainsKey(id)) return "Write target missing from read set.";
        }
        if (reads is not null && (plan.Creates.Count + plan.Removes.Count > 0 ||
            plan.Updates.Any(p => existing[p.PosterId].Poster.StackOrder != p.StackOrder)) && reads.CatalogVersion is null)
            return "Catalog mutation requires the catalog version.";
        if (plan.NewContinuity is { } continuity && (reads?.CatalogVersion != before.Catalog.CatalogVersion ||
            !reads.Posters.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(existing.Keys) ||
            continuity.Nodes.Any(n => !existing.ContainsKey(n.Expected.PosterId)) || plan.Updates.Count == 0))
            return "Continuity requires a complete catalog read set and a spatial entity mutation.";
        if (plan.Settings is { } settings)
        {
            if (reads is not null && reads.SettingsVersion is null) return "Settings version missing from read set.";
            return AuthoritativeStateValidationPolicy.Validate(settings);
        }
        return null;
    }

    internal static void AdvanceHead(SqliteConnection connection, SqliteTransaction transaction, long previous, string operationId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE state_head SET revision = $next, last_operation_id = $id, updated_utc = $utc
                WHERE singleton_id = 1 AND revision = $previous;
            """;
        command.Parameters.AddWithValue("$next", checked(previous + 1));
        command.Parameters.AddWithValue("$previous", previous);
        command.Parameters.AddWithValue("$id", operationId);
        command.Parameters.AddWithValue("$utc", SqlitePosterRepository.TimestampNow());
        if (command.ExecuteNonQuery() != 1) throw new InvalidDataException("Global revision changed during transaction.");
        command.CommandText = "INSERT INTO state_operations VALUES ($id, $previous, $next, $utc);";
        command.ExecuteNonQuery();
    }
}
