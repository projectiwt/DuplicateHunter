using DuplicateHunter.Database;
using Xunit;

namespace DuplicateHunter.Tests;

public class DatabaseServiceTests
{
    [Fact]
    public async Task DatabaseService_PersistsRecentFoldersAndScanHistory()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "DuplicateHunter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        string databasePath = Path.Combine(tempDirectory, "duplicate-hunter.db");
        using var service = new SqliteDuplicateHunterDatabaseService(new DuplicateHunterDatabaseOptions(databasePath));

        try
        {
            await service.InitializeAsync();
            await service.AddRecentFolderAsync("C:\\Data\\Projects");
            await service.SaveScanHistoryAsync(new ScanHistoryRecord(
                "C:\\Data\\Projects",
                DateTime.UtcNow.AddMinutes(-1),
                DateTime.UtcNow,
                "Completed",
                42,
                3,
                1048576));
            await service.SaveReportAsync(new ReportRecord(
                "C:\\Data\\Projects",
                "C:\\Reports\\report.xlsx",
                "xlsx",
                DateTime.UtcNow,
                42,
                3,
                1048576));

            var recentFolders = await service.GetRecentFoldersAsync();
            var settingBefore = await service.GetSettingAsync("Theme");
            await service.SetSettingAsync("Theme", "Light");
            var settingAfter = await service.GetSettingAsync("Theme");

            var history = await service.GetScanHistoryAsync();
            var reports = await service.GetReportsAsync();

            Assert.Single(recentFolders);
            Assert.Equal("C:\\Data\\Projects", recentFolders[0].FolderPath);
            Assert.Equal(1, recentFolders[0].UsageCount);
            Assert.Null(settingBefore);
            Assert.Equal("Light", settingAfter);
            Assert.True(File.Exists(databasePath));

            Assert.Single(history);
            Assert.Equal("C:\\Data\\Projects", history[0].FolderPath);
            Assert.Equal("Completed", history[0].Status);
            Assert.Equal(42, history[0].FilesScanned);

            Assert.Single(reports);
            Assert.Equal("xlsx", reports[0].ReportFormat);
            Assert.Equal("C:\\Reports\\report.xlsx", reports[0].ReportPath);

            // Test clearing history
            await service.ClearScanHistoryAsync();
            await service.ClearReportsAsync();

            var clearedHistory = await service.GetScanHistoryAsync();
            var clearedReports = await service.GetReportsAsync();

            Assert.Empty(clearedHistory);
            Assert.Empty(clearedReports);
        }
        finally
        {
            service.Dispose();

            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
