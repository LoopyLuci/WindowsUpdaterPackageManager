using System.Text.Json;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Data;

public sealed class SqliteStateDatabase : IStateDatabase
{
    private readonly string _connectionString;
    private const string Schema = "CREATE TABLE IF NOT EXISTS InstalledPackages (" +
        "Id TEXT PRIMARY KEY, Version TEXT NOT NULL, DisplayName TEXT NOT NULL, " +
        "Architecture TEXT, InstalledAt TEXT NOT NULL, IsDriver INTEGER NOT NULL" +
    ");";

    public SqliteStateDatabase(string rootPath)
    {
        _connectionString = new System.Data.SQLite.SQLiteConnectionStringBuilder
        {
            DataSource = System.IO.Path.Combine(rootPath, "wupm-state.db")
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new System.Data.SQLite.SQLiteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Schema;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PackageManifest>> ListInstalledAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new System.Data.SQLite.SQLiteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Version, DisplayName, Architecture, InstalledAt, IsDriver FROM InstalledPackages";
        var list = new List<PackageManifest>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new PackageManifest
            {
                Id = reader.GetString(0),
                Version = reader.GetString(1),
                DisplayName = reader.GetString(2),
                Architecture = reader.IsDBNull(3) ? null : reader.GetString(3),
                PublishedAt = DateTimeOffset.Parse(reader.GetString(4)),
                IsDriver = reader.GetInt32(5) != 0
            });
        }
        return list;
    }

    public async Task RecordInstallAsync(PackageManifest package, CancellationToken cancellationToken = default)
    {
        await using var connection = new System.Data.SQLite.SQLiteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO InstalledPackages (Id, Version, DisplayName, Architecture, InstalledAt, IsDriver) VALUES (@id, @version, @displayName, @arch, @installedAt, @isDriver)";
        cmd.Parameters.AddWithValue("@id", package.Id);
        cmd.Parameters.AddWithValue("@version", package.Version);
        cmd.Parameters.AddWithValue("@displayName", package.DisplayName);
        cmd.Parameters.AddWithValue("@arch", (object?)package.Architecture ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@installedAt", package.PublishedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@isDriver", package.IsDriver ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveInstallAsync(string packageId, CancellationToken cancellationToken = default)
    {
        await using var connection = new System.Data.SQLite.SQLiteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM InstalledPackages WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", packageId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsInstalledAsync(string packageId, string? version = null, CancellationToken cancellationToken = default)
    {
        await using var connection = new System.Data.SQLite.SQLiteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = version is null
            ? "SELECT COUNT(1) FROM InstalledPackages WHERE Id = @id"
            : "SELECT COUNT(1) FROM InstalledPackages WHERE Id = @id AND Version = @version";
        cmd.Parameters.AddWithValue("@id", packageId);
        if (version is not null)
        {
            cmd.Parameters.AddWithValue("@version", version);
        }
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        return count > 0;
    }
}
