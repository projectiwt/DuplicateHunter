namespace DuplicateHunter.Database;

public interface IDuplicateHunterDatabaseService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SaveScanHistoryAsync(ScanHistoryRecord record, CancellationToken cancellationToken = default);
    Task SaveReportAsync(ReportRecord record, CancellationToken cancellationToken = default);
    Task AddRecentFolderAsync(string folderPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecentFolderRecord>> GetRecentFoldersAsync(int limit = 10, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScanHistoryRecord>> GetScanHistoryAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportRecord>> GetReportsAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task ClearScanHistoryAsync(CancellationToken cancellationToken = default);
    Task ClearReportsAsync(CancellationToken cancellationToken = default);
    Task SetSettingAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default);
}
