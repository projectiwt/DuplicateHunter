using DuplicateHunter.Database;
using DuplicateHunter.Models;
using DuplicateHunter.Services;
using System.Text;
using Xunit;

namespace DuplicateHunter.Tests;

public class FullWorkflowIntegrationTests
{
    [Fact]
    public async Task FullScanningFilteringReportingAndDatabaseWorkflow_ExecutesSuccessfully()
    {
        string rootTemp = Path.Combine(Path.GetTempPath(), "DuplicateHunter.E2E", Guid.NewGuid().ToString("N"));
        string scanDir = Path.Combine(rootTemp, "ScanTarget");
        string reportsDir = Path.Combine(rootTemp, "Reports");
        string dbPath = Path.Combine(rootTemp, "duplicate-hunter.db");

        Directory.CreateDirectory(scanDir);
        Directory.CreateDirectory(reportsDir);

        // Create test file structure
        // Group 1: 3 duplicate text files
        string fileA1 = Path.Combine(scanDir, "doc_original.txt");
        string fileA2 = Path.Combine(scanDir, "doc_copy1.txt");
        string subDir = Path.Combine(scanDir, "NestedSubFolder");
        Directory.CreateDirectory(subDir);
        string fileA3 = Path.Combine(subDir, "doc_copy2.txt");

        byte[] payloadA = Encoding.UTF8.GetBytes("DUPLICATE_PAYLOAD_ALPHA_1234567890");
        await File.WriteAllBytesAsync(fileA1, payloadA);
        await File.WriteAllBytesAsync(fileA2, payloadA);
        await File.WriteAllBytesAsync(fileA3, payloadA);

        // Group 2: 2 duplicate binary files
        string fileB1 = Path.Combine(scanDir, "image_original.jpg");
        string fileB2 = Path.Combine(subDir, "image_backup.jpg");
        byte[] payloadB = new byte[1024 * 10]; // 10 KB
        Random.Shared.NextBytes(payloadB);
        await File.WriteAllBytesAsync(fileB1, payloadB);
        await File.WriteAllBytesAsync(fileB2, payloadB);

        // 1 Unique file
        string uniqueFile = Path.Combine(scanDir, "unique_record.pdf");
        await File.WriteAllBytesAsync(uniqueFile, Encoding.UTF8.GetBytes("UNIQUE_FILE_CONTENT_ONLY_ONE_INSTANCE"));

        // Initialize Services
        var enumService = new FileEnumerationService();
        var hashService = new HashService();
        var scannerService = new FileScannerService(enumService, hashService);
        var groupingService = new DuplicateGroupingService();
        var statsService = new ScanStatisticsService();
        var exporter = new ReportExportService();
        using var dbService = new SqliteDuplicateHunterDatabaseService(new DuplicateHunterDatabaseOptions(dbPath));
        await dbService.InitializeAsync();
        var settingsService = new AppSettingsService(dbService);

        try
        {
            // 1. Test Enumeration
            var enumerated = enumService.GetFiles(scanDir);
            Assert.Equal(6, enumerated.Count);

            // 2. Test Scan
            var progressReports = new List<ScanProgress>();
            var progress = new Progress<ScanProgress>(p => progressReports.Add(p));
            var scannedFiles = await scannerService.ScanAsync(scanDir, progress);
            Assert.Equal(6, scannedFiles.Count);

            // 3. Test Grouping
            var groups = groupingService.GroupDuplicates(scannedFiles);
            Assert.Equal(2, groups.Count);

            var alphaGroup = groups.FirstOrDefault(g => g.FileCount == 3);
            var betaGroup = groups.FirstOrDefault(g => g.FileCount == 2);
            Assert.NotNull(alphaGroup);
            Assert.NotNull(betaGroup);

            // 4. Test Statistics
            var stats = statsService.Calculate(scannedFiles);
            Assert.Equal(6, stats.FilesScanned);
            Assert.Equal(2, stats.DuplicateGroups);
            long expectedWasted = (2 * payloadA.Length) + (1 * payloadB.Length);
            Assert.Equal(expectedWasted, stats.WastedBytes);

            // 5. Test Exporting All 4 Formats
            string csvPath = Path.Combine(reportsDir, "report.csv");
            string jsonPath = Path.Combine(reportsDir, "report.json");
            string excelPath = Path.Combine(reportsDir, "report.xlsx");
            string pdfPath = Path.Combine(reportsDir, "report.pdf");

            await exporter.ExportToCsvAsync(csvPath, groups, stats);
            await exporter.ExportToJsonAsync(jsonPath, groups, stats);
            await exporter.ExportToExcelAsync(excelPath, groups, stats);
            await exporter.ExportToPdfAsync(pdfPath, groups, stats);

            Assert.True(File.Exists(csvPath) && new FileInfo(csvPath).Length > 0);
            Assert.True(File.Exists(jsonPath) && new FileInfo(jsonPath).Length > 0);
            Assert.True(File.Exists(excelPath) && new FileInfo(excelPath).Length > 0);
            Assert.True(File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 0);

            // 6. Test Database Persistence
            await dbService.SaveScanHistoryAsync(new ScanHistoryRecord(
                scanDir, DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow, "Completed", stats.FilesScanned, stats.DuplicateGroups, stats.WastedBytes));

            await dbService.SaveReportAsync(new ReportRecord(
                scanDir, csvPath, "csv", DateTime.UtcNow, stats.FilesScanned, stats.DuplicateGroups, stats.WastedBytes));
            await dbService.SaveReportAsync(new ReportRecord(
                scanDir, pdfPath, "pdf", DateTime.UtcNow, stats.FilesScanned, stats.DuplicateGroups, stats.WastedBytes));

            await dbService.AddRecentFolderAsync(scanDir);

            var history = await dbService.GetScanHistoryAsync();
            var reports = await dbService.GetReportsAsync();
            var recent = await dbService.GetRecentFoldersAsync();

            Assert.Single(history);
            Assert.Equal("Completed", history[0].Status);
            Assert.Equal(2, reports.Count);
            Assert.Single(recent);

            // 7. Test Settings Persistence
            var defaultSettings = await settingsService.LoadAsync();
            var customSettings = new ScanSettings
            {
                Theme = "Dark",
                IgnoreFoldersText = "node_modules, bin, obj",
                IgnoreExtensionsText = ".tmp, .dll",
                DefaultMinimumFileSizeMB = 0.5,
                DefaultMaximumFileSizeMB = 500
            };
            await settingsService.SaveAsync(customSettings);

            var reloadedSettings = await settingsService.LoadAsync();
            Assert.Equal("Dark", reloadedSettings.Theme);
            Assert.Equal("node_modules, bin, obj", reloadedSettings.IgnoreFoldersText);
            Assert.Equal(0.5, reloadedSettings.DefaultMinimumFileSizeMB);
        }
        finally
        {
            dbService.Dispose();
            if (Directory.Exists(rootTemp))
            {
                Directory.Delete(rootTemp, recursive: true);
            }
        }
    }
}
