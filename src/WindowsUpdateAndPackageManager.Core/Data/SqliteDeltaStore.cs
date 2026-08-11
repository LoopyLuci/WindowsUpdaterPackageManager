using Microsoft.Data.Sqlite;
using WindowsUpdateAndPackageManager.Core;

namespace WindowsUpdateAndPackageManager.Data;

public sealed class SqliteDeltaStore : IDeltaStore
{
    private readonly string _connectionString;
    private const string Schema = "CREATE TABLE IF NOT EXISTS Deltas (" +
        "PackageId TEXT NOT NULL, FromVersion TEXT NOT NULL, ToVersion TEXT NOT NULL, " +
        "DeltaUrl TEXT NOT NULL, DeltaSize INTEGER NOT NULL, DeltaHash TEXT NOT NULL, " +
        "PRIMARY KEY(PackageId, FromVersion, ToVersion));";

    public SqliteDeltaStore(string rootPath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(rootPath, "wupm-deltas.db")
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Schema;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(DeltaManifest manifest, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO Deltas (PackageId, FromVersion, ToVersion, DeltaUrl, DeltaSize, DeltaHash) VALUES (@packageId, @fromVersion, @toVersion, @deltaUrl, @deltaSize, @deltaHash)";
        cmd.Parameters.AddWithValue("@packageId", manifest.PackageId);
        cmd.Parameters.AddWithValue("@fromVersion", manifest.FromVersion);
        cmd.Parameters.AddWithValue("@toVersion", manifest.ToVersion);
        cmd.Parameters.AddWithValue("@deltaUrl", manifest.DeltaUrl);
        cmd.Parameters.AddWithValue("@deltaSize", manifest.DeltaSize);
        cmd.Parameters.AddWithValue("@deltaHash", manifest.DeltaHash);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeltaManifest?> GetAsync(string packageId, string fromVersion, string toVersion, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT PackageId, FromVersion, ToVersion, DeltaUrl, DeltaSize, DeltaHash FROM Deltas WHERE PackageId=@packageId AND FromVersion=@fromVersion AND ToVersion=@toVersion";
        cmd.Parameters.AddWithValue("@packageId", packageId);
        cmd.Parameters.AddWithValue("@fromVersion", fromVersion);
        cmd.Parameters.AddWithValue("@toVersion", toVersion);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;

        return new DeltaManifest
        {
            PackageId = reader.GetString(0),
            FromVersion = reader.GetString(1),
            ToVersion = reader.GetString(2),
            DeltaUrl = reader.GetString(3),
            DeltaSize = reader.GetInt64(4),
            DeltaHash = reader.GetString(5)
        };
    }

    public async Task<IReadOnlyList<DeltaManifest>> ListAsync(string packageId, string? toVersion = null, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        var sql = "SELECT PackageId, FromVersion, ToVersion, DeltaUrl, DeltaSize, DeltaHash FROM Deltas WHERE PackageId=@packageId";
        if (toVersion is not null)
        {
            sql += " AND ToVersion=@toVersion";
        }
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@packageId", packageId);
        if (toVersion is not null) cmd.Parameters.AddWithValue("@toVersion", toVersion);

        var list = new List<DeltaManifest>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new DeltaManifest
            {
                PackageId = reader.GetString(0),
                FromVersion = reader.GetString(1),
                ToVersion = reader.GetString(2),
                DeltaUrl = reader.GetString(3),
                DeltaSize = reader.GetInt64(4),
                DeltaHash = reader.GetString(5)
            });
        }
        return list;
    }
}
