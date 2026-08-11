using System.Text.Json;
using Microsoft.Data.Sqlite;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Data;

public sealed class SqliteAuditStore : IAuditStore
{
    private readonly string _connectionString;
    private bool _initialized;
    private static readonly string Schema = "CREATE TABLE IF NOT EXISTS AuditEntries (" +
        "Id TEXT PRIMARY KEY, Timestamp TEXT NOT NULL, Action TEXT NOT NULL, " +
        "PackageId TEXT, Version TEXT, ComputerName TEXT, UserName TEXT, " +
        "Success INTEGER NOT NULL, Message TEXT" +
    "); CREATE INDEX IF NOT EXISTS IX_AuditEntries_Timestamp ON AuditEntries(Timestamp);" +
    " CREATE INDEX IF NOT EXISTS IX_AuditEntries_PackageId ON AuditEntries(PackageId);";

    public SqliteAuditStore(string rootPath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(rootPath, "wupm-audit.db")
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Schema;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        _initialized = true;
    }

    public async Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO AuditEntries (Id, Timestamp, Action, PackageId, Version, ComputerName, UserName, Success, Message) VALUES (@id, @timestamp, @action, @packageId, @version, @computerName, @userName, @success, @message)";
        cmd.Parameters.AddWithValue("@id", entry.Id.ToString());
        cmd.Parameters.AddWithValue("@timestamp", entry.Timestamp.ToString("o"));
        cmd.Parameters.AddWithValue("@action", entry.Action);
        cmd.Parameters.AddWithValue("@packageId", (object?)entry.PackageId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@version", (object?)entry.Version ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@computerName", (object?)entry.ComputerName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@userName", (object?)entry.User ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@success", entry.Success ? 1 : 0);
        cmd.Parameters.AddWithValue("@message", (object?)entry.Message ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AuditEntry>> QueryAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, string? action = null, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var where = new List<string>();
        if (from is not null) where.Add("Timestamp >= @from");
        if (to is not null) where.Add("Timestamp <= @to");
        if (!string.IsNullOrEmpty(action)) where.Add("Action = @action");
        var whereSql = where.Count == 0 ? "" : " WHERE " + string.Join(" AND ", where);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT Id, Timestamp, Action, PackageId, Version, ComputerName, UserName, Success, Message FROM AuditEntries{whereSql} ORDER BY Timestamp DESC";
        if (from is not null) cmd.Parameters.AddWithValue("@from", from.Value.ToString("o"));
        if (to is not null) cmd.Parameters.AddWithValue("@to", to.Value.ToString("o"));
        if (!string.IsNullOrEmpty(action)) cmd.Parameters.AddWithValue("@action", action);

        var list = new List<AuditEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new AuditEntry
            {
                Id = Guid.Parse(reader.GetString(0)),
                Timestamp = DateTimeOffset.Parse(reader.GetString(1)),
                Action = reader.GetString(2),
                PackageId = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Version = reader.IsDBNull(4) ? null : reader.GetString(4),
                ComputerName = reader.IsDBNull(5) ? null : reader.GetString(5),
                User = reader.IsDBNull(6) ? null : reader.GetString(6),
                Success = reader.GetInt32(7) != 0,
                Message = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }
        return list;
    }
}
