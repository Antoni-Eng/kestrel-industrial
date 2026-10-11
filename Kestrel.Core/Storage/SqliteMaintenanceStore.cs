using Kestrel.Core.Abstractions;
using Kestrel.Core.Configuration;
using Kestrel.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Kestrel.Core.Storage;

public sealed class SqliteMaintenanceStore(IOptions<RagOptions> options) : IMaintenanceStore
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = Path.GetFullPath(options.Value.SqlitePath),
        Mode = SqliteOpenMode.ReadWriteCreate
    }.ToString();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Value.SqlitePath))!);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS equipment (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                model TEXT NOT NULL,
                type TEXT NOT NULL,
                plant_id TEXT NOT NULL,
                area TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS plants (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                location TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS maintenance_logs (
                id INTEGER PRIMARY KEY,
                equipment_id TEXT NOT NULL,
                performed_at TEXT NOT NULL,
                technician TEXT NOT NULL,
                summary TEXT NOT NULL,
                work_type TEXT NOT NULL,
                hours INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS work_orders (
                id INTEGER PRIMARY KEY,
                equipment_id TEXT NOT NULL,
                plant_id TEXT NOT NULL,
                title TEXT NOT NULL,
                status TEXT NOT NULL,
                priority TEXT NOT NULL,
                opened_at TEXT NOT NULL,
                due_at TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS feedback (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                conversation_id TEXT NOT NULL,
                message_id TEXT NOT NULL,
                helpful INTEGER NOT NULL,
                comment TEXT NULL,
                trace_id TEXT NULL,
                user_id TEXT NULL,
                user_email TEXT NULL,
                created_at TEXT NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        await EnsureColumnAsync(connection, "feedback", "user_id", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "feedback", "user_email", "TEXT", cancellationToken);
    }

    public async Task<IReadOnlyList<Equipment>> GetEquipmentAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, model, type, plant_id, area FROM equipment ORDER BY id";
        var list = new List<Equipment>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new Equipment(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5)));
        }

        return list;
    }

    public async Task<Equipment?> GetEquipmentAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, model, type, plant_id, area FROM equipment WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new Equipment(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5));
    }

    public async Task<IReadOnlyList<MaintenanceLog>> GetMaintenanceHistoryAsync(
        string equipmentId,
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, equipment_id, performed_at, technician, summary, work_type, hours
            FROM maintenance_logs
            WHERE equipment_id = $id
            ORDER BY performed_at DESC
            LIMIT $take
            """;
        cmd.Parameters.AddWithValue("$id", equipmentId);
        cmd.Parameters.AddWithValue("$take", take);

        var list = new List<MaintenanceLog>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new MaintenanceLog(
                reader.GetInt64(0),
                reader.GetString(1),
                DateTime.Parse(reader.GetString(2)),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6)));
        }

        return list;
    }

    public async Task<IReadOnlyList<WorkOrder>> GetOpenWorkOrdersAsync(
        string? plantId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, equipment_id, plant_id, title, status, priority, opened_at, due_at
            FROM work_orders
            WHERE status IN ('Open', 'In Progress')
              AND ($plant IS NULL OR plant_id = $plant)
            ORDER BY opened_at DESC
            """;
        cmd.Parameters.AddWithValue("$plant", string.IsNullOrWhiteSpace(plantId) ? DBNull.Value : plantId);

        var list = new List<WorkOrder>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new WorkOrder(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                DateTime.Parse(reader.GetString(6)),
                reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7))));
        }

        return list;
    }

    public async Task SaveFeedbackAsync(FeedbackRequest feedback, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO feedback (conversation_id, message_id, helpful, comment, trace_id, user_id, user_email, created_at)
            VALUES ($c, $m, $h, $comment, $t, $uid, $email, $created)
            """;
        cmd.Parameters.AddWithValue("$c", feedback.ConversationId);
        cmd.Parameters.AddWithValue("$m", feedback.MessageId);
        cmd.Parameters.AddWithValue("$h", feedback.Helpful ? 1 : 0);
        cmd.Parameters.AddWithValue("$comment", (object?)feedback.Comment ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$t", (object?)feedback.TraceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$uid", (object?)feedback.UserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$email", (object?)feedback.UserEmail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string table,
        string column,
        string type,
        CancellationToken cancellationToken)
    {
        var exists = false;
        await using (var check = connection.CreateCommand())
        {
            check.CommandText = $"PRAGMA table_info({table})";
            await using var reader = await check.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists)
        {
            return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {type}";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
