using System.Text.Json;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

internal static class SqliteSpatialContinuityRepository
{
    public static IReadOnlyList<SpatialContinuityState> Read(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT workspace_id, body_json FROM spatial_continuity ORDER BY workspace_id;";
        using var reader = command.ExecuteReader(); var entries = new List<SpatialContinuityState>();
        while (reader.Read())
        {
            var state = JsonSerializer.Deserialize<SpatialContinuityState>(reader.GetString(1));
            if (state is null || state.SchemaVersion != 1 || state.Environment is null || state.Nodes is null ||
                state.Nodes.Any(n => n is null || n.Expected is null) || state.Nodes.Count > 256 ||
                state.Environment.WorkspaceId != reader.GetString(0)) throw new InvalidDataException("Invalid continuity state.");
            entries.Add(state with { Nodes = Array.AsReadOnly(state.Nodes.ToArray()) });
        }
        return entries.AsReadOnly();
    }

    public static void Write(SqliteConnection connection, SqliteTransaction transaction, SpatialContinuityState state, long revision)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO spatial_continuity(workspace_id, schema_version, body_json, committed_revision)
            VALUES ($workspace, 1, $json, $revision)
            ON CONFLICT(workspace_id) DO UPDATE SET body_json = excluded.body_json,
                committed_revision = excluded.committed_revision;
            """;
        command.Parameters.AddWithValue("$workspace", state.Environment.WorkspaceId);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(state));
        command.Parameters.AddWithValue("$revision", revision); command.ExecuteNonQuery();
    }
}
