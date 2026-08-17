namespace DuplicateHunter.Database;

public sealed record ScanHistoryRecord(
    string FolderPath,
    DateTime StartedUtc,
    DateTime CompletedUtc,
    string Status,
    int FilesScanned,
    int DuplicateGroups,
    long WastedBytes,
    string? ErrorMessage = null);

public sealed record RecentFolderRecord(
    string FolderPath,
    DateTime LastUsedUtc,
    int UsageCount);

public sealed record ReportRecord(
    string ScanFolderPath,
    string ReportPath,
    string ReportFormat,
    DateTime CreatedUtc,
    int FilesScanned,
    int DuplicateGroups,
    long WastedBytes);

public sealed record UserSettingRecord(
    string SettingKey,
    string SettingValue,
    DateTime UpdatedUtc);
