using System.Text.Json;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

internal static class SqliteSpatialProofRepository
{
    public static IReadOnlyList<SpatialOperationProof> Read(SqliteConnection connection, SqliteTransaction transaction)
    {
        var operations = new List<SpatialOperationProof>();
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT body_json FROM spatial_operations WHERE status = 'active' ORDER BY operation_id;";
        using (var reader = command.ExecuteReader())
            while (reader.Read()) operations.Add(JsonSerializer.Deserialize<SpatialOperationProof>(reader.GetString(0))
                ?? throw new InvalidDataException("Invalid spatial proof."));
        for (int i = 0; i < operations.Count; i++)
        {
            var entries = new List<SpatialReturnProof>();
            command.CommandText = "SELECT proof_json FROM spatial_participants WHERE operation_id = $id ORDER BY poster_id;";
            command.Parameters.Clear(); command.Parameters.AddWithValue("$id", operations[i].OperationId);
            using var reader = command.ExecuteReader();
            while (reader.Read()) entries.Add(JsonSerializer.Deserialize<SpatialReturnProof>(reader.GetString(0))
                ?? throw new InvalidDataException("Invalid spatial participant."));
            operations[i] = operations[i] with { Participants = entries.AsReadOnly() };
        }
        return operations.AsReadOnly();
    }

    public static void Invalidate(SqliteConnection connection, SqliteTransaction transaction, string posterId)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            UPDATE spatial_operations SET status = 'invalidated' WHERE source_poster_id = $id AND status = 'active';
            UPDATE spatial_participants SET invalidated_reason = 'layout-generation-changed' WHERE poster_id = $id AND invalidated_reason IS NULL;
            """;
        command.Parameters.AddWithValue("$id", posterId); command.ExecuteNonQuery();
    }

    public static void Apply(SqliteConnection connection, SqliteTransaction transaction, SpatialOperationProof? created, string? consumed, long revision)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        if (consumed is not null)
        {
            command.CommandText = "UPDATE spatial_operations SET status = 'consumed' WHERE operation_id = $id AND status != 'consumed';";
            command.Parameters.AddWithValue("$id", consumed);
            if (command.ExecuteNonQuery() != 1) throw new InvalidDataException("Spatial proof was already consumed or missing.");
        }
        if (created is null) return;
        command.Parameters.Clear();
        command.CommandText = """
            INSERT INTO spatial_operations (operation_id, source_poster_id, schema_version, status, body_json, committed_revision)
            VALUES ($id, $source, 1, 'active', $json, $revision);
            """;
        command.Parameters.AddWithValue("$id", created.OperationId);
        command.Parameters.AddWithValue("$source", created.ExpandedSource.PosterId);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(created with { Participants = Array.Empty<SpatialReturnProof>() }));
        command.Parameters.AddWithValue("$revision", revision); command.ExecuteNonQuery();
        foreach (var participant in created.Participants)
        {
            command.Parameters.Clear();
            command.CommandText = """
                INSERT INTO spatial_participants(operation_id, poster_id, expected_layout_generation, proof_json)
                VALUES ($id, $poster, $generation, $json);
                """;
            command.Parameters.AddWithValue("$id", created.OperationId);
            command.Parameters.AddWithValue("$poster", participant.TargetAfter.PosterId);
            command.Parameters.AddWithValue("$generation", participant.TargetAfter.LayoutGeneration);
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(participant)); command.ExecuteNonQuery();
        }
    }
}
