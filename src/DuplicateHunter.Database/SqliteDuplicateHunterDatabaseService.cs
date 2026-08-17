using Microsoft.Data.Sqlite;

namespace DuplicateHunter.Database;

public sealed class SqliteDuplicateHunterDatabaseService : IDuplicateHunterDatabaseService, IDisposable
{
    private readonly DuplicateHunterDatabaseOptions _options;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private bool _initialized;

    public SqliteDuplicateHunterDatabaseService(DuplicateHunterDatabaseOptions options)
    {
        _options = options;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        await _initializeLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
                return;

            string? directory = Path.GetDirectoryName(_options.DatabasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await EnsureSchemaAsync(connection, cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    public async Task SaveScanHistoryAsync(ScanHistoryRecord record, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO ScanHistory
(FolderPath, StartedUtc, CompletedUtc, Status, FilesScanned, DuplicateGroups, WastedBytes, ErrorMessage)
VALUES
($folderPath, $startedUtc, $completedUtc, $status, $filesScanned, $duplicateGroups, $wastedBytes, $errorMessage);";

        command.Parameters.AddWithValue("$folderPath", record.FolderPath);
        command.Parameters.AddWithValue("$startedUtc", record.StartedUtc.ToString("O"));
        command.Parameters.AddWithValue("$completedUtc", record.CompletedUtc.ToString("O"));
        command.Parameters.AddWithValue("$status", record.Status);
        command.Parameters.AddWithValue("$filesScanned", record.FilesScanned);
        command.Parameters.AddWithValue("$duplicateGroups", record.DuplicateGroups);
        command.Parameters.AddWithValue("$wastedBytes", record.WastedBytes);
        command.Parameters.AddWithValue("$errorMessage", (object?)record.ErrorMessage ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveReportAsync(ReportRecord record, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO Reports
(ScanFolderPath, ReportPath, ReportFormat, CreatedUtc, FilesScanned, DuplicateGroups, WastedBytes)
VALUES
($scanFolderPath, $reportPath, $reportFormat, $createdUtc, $filesScanned, $duplicateGroups, $wastedBytes);";

        command.Parameters.AddWithValue("$scanFolderPath", record.ScanFolderPath);
        command.Parameters.AddWithValue("$reportPath", record.ReportPath);
        command.Parameters.AddWithValue("$reportFormat", record.ReportFormat);
        command.Parameters.AddWithValue("$createdUtc", record.CreatedUtc.ToString("O"));
        command.Parameters.AddWithValue("$filesScanned", record.FilesScanned);
        command.Parameters.AddWithValue("$duplicateGroups", record.DuplicateGroups);
        command.Parameters.AddWithValue("$wastedBytes", record.WastedBytes);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddRecentFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO RecentFolders (FolderPath, LastUsedUtc, UsageCount)
VALUES ($folderPath, $lastUsedUtc, 1)
ON CONFLICT(FolderPath) DO UPDATE SET
    LastUsedUtc = excluded.LastUsedUtc,
    UsageCount = UsageCount + 1;";

        command.Parameters.AddWithValue("$folderPath", folderPath);
        command.Parameters.AddWithValue("$lastUsedUtc", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecentFolderRecord>> GetRecentFoldersAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT FolderPath, LastUsedUtc, UsageCount
FROM RecentFolders
ORDER BY LastUsedUtc DESC
LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));

        var folders = new List<RecentFolderRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            folders.Add(new RecentFolderRecord(
                reader.GetString(0),
                DateTime.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.GetInt32(2)));
        }

        return folders;
    }

    public async Task<IReadOnlyList<ScanHistoryRecord>> GetScanHistoryAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT FolderPath, StartedUtc, CompletedUtc, Status, FilesScanned, DuplicateGroups, WastedBytes, ErrorMessage
FROM ScanHistory
ORDER BY CompletedUtc DESC
LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));

        var records = new List<ScanHistoryRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new ScanHistoryRecord(
                reader.GetString(0),
                DateTime.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind),
                DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt64(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return records;
    }

    public async Task<IReadOnlyList<ReportRecord>> GetReportsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT ScanFolderPath, ReportPath, ReportFormat, CreatedUtc, FilesScanned, DuplicateGroups, WastedBytes
FROM Reports
ORDER BY CreatedUtc DESC
LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));

        var records = new List<ReportRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new ReportRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt64(6)));
        }

        return records;
    }

    public async Task ClearScanHistoryAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ScanHistory;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearReportsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Reports;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetSettingAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO UserSettings (SettingKey, SettingValue, UpdatedUtc)
VALUES ($key, $value, $updatedUtc)
ON CONFLICT(SettingKey) DO UPDATE SET
    SettingValue = excluded.SettingValue,
    UpdatedUtc = excluded.UpdatedUtc;";

        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$updatedUtc", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT SettingValue
FROM UserSettings
WHERE SettingKey = $key
LIMIT 1;";
        command.Parameters.AddWithValue("$key", key);

        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return result?.ToString();
    }

    private SqliteConnection CreateConnection() => new($"Data Source={_options.DatabasePath}");

    private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS ScanHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FolderPath TEXT NOT NULL,
    StartedUtc TEXT NOT NULL,
    CompletedUtc TEXT NOT NULL,
    Status TEXT NOT NULL,
    FilesScanned INTEGER NOT NULL,
    DuplicateGroups INTEGER NOT NULL,
    WastedBytes INTEGER NOT NULL,
    ErrorMessage TEXT NULL
);

CREATE TABLE IF NOT EXISTS RecentFolders (
    FolderPath TEXT PRIMARY KEY,
    LastUsedUtc TEXT NOT NULL,
    UsageCount INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS Reports (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ScanFolderPath TEXT NOT NULL,
    ReportPath TEXT NOT NULL,
    ReportFormat TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL,
    FilesScanned INTEGER NOT NULL,
    DuplicateGroups INTEGER NOT NULL,
    WastedBytes INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS UserSettings (
    SettingKey TEXT PRIMARY KEY,
    SettingValue TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL
);";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _initializeLock.Dispose();
    }
}
