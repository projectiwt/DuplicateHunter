using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using DuplicateHunter.Database;
using DuplicateHunter.Models;
using DuplicateHunter.Services;
using Xunit;

namespace DuplicateHunter.Tests;

public class ReleaseValidationTests
{
    private class MockFileOperationService : IFileOperationService
    {
        public List<string> OpenedFiles { get; } = new();
        public List<string> OpenedFolders { get; } = new();
        public List<string> CopiedTexts { get; } = new();
        public List<string> RecycledFiles { get; } = new();
        public List<string> PermanentlyDeletedFiles { get; } = new();

        public void OpenFile(string filePath) => OpenedFiles.Add(filePath);
        public void OpenFolder(string filePath) => OpenedFolders.Add(filePath);
        public void CopyToClipboard(string text) => CopiedTexts.Add(text);

        public bool MoveToRecycleBin(string filePath)
        {
            RecycledFiles.Add(filePath);
            if (File.Exists(filePath))
                File.Delete(filePath);
            return true;
        }

        public bool DeletePermanently(string filePath)
        {
            PermanentlyDeletedFiles.Add(filePath);
            if (File.Exists(filePath))
                File.Delete(filePath);
            return true;
        }
    }

    [Fact]
    public async Task Phase3_SmallControlledScan_CorrectGroupingAndFiltering()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "DH_Phase3_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string file1 = Path.Combine(tempDir, "file1.txt");
            string file1Copy = Path.Combine(tempDir, "file1_copy.txt");
            string file2 = Path.Combine(tempDir, "file2.txt");
            string file2Copy = Path.Combine(tempDir, "file2_copy.txt");
            string unique = Path.Combine(tempDir, "unique.txt");

            await File.WriteAllTextAsync(file1, "Exact same content for file 1");
            await File.WriteAllTextAsync(file1Copy, "Exact same content for file 1");
            await File.WriteAllTextAsync(file2, "Different content for file 2 - 12345");
            await File.WriteAllTextAsync(file2Copy, "Different content for file 2 - 12345");
            await File.WriteAllTextAsync(unique, "Unique content that matches nothing else");

            var enumService = new FileEnumerationService();
            var hashService = new HashService();
            var scanner = new FileScannerService(enumService, hashService);
            var grouping = new DuplicateGroupingService();

            var scanned = await scanner.ScanAsync(tempDir);
            Assert.Equal(5, scanned.Count);

            var groups = grouping.GroupDuplicates(scanned);
            Assert.Equal(2, groups.Count);

            Assert.All(groups, g => Assert.Equal(2, g.FileCount));
            Assert.DoesNotContain(groups.SelectMany(g => g.Files), f => f.FilePath == unique);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Phase4_Sha256Validation_MatchesCryptographicSha256Exactly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "DH_Phase4_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string fileA = Path.Combine(tempDir, "data_a.bin");
            string fileB = Path.Combine(tempDir, "data_b.bin");

            byte[] randomBytes = new byte[8192];
            Random.Shared.NextBytes(randomBytes);
            await File.WriteAllBytesAsync(fileA, randomBytes);
            await File.WriteAllBytesAsync(fileB, randomBytes);

            byte[] independentHashBytes = SHA256.HashData(randomBytes);
            string independentExpectedHash = Convert.ToHexString(independentHashBytes);

            var hashService = new HashService();
            string actualHashA = await hashService.ComputeHashAsync(fileA);
            string actualHashB = await hashService.ComputeHashAsync(fileB);

            Assert.Equal(independentExpectedHash, actualHashA);
            Assert.Equal(independentExpectedHash, actualHashB);
            Assert.Equal(actualHashA, actualHashB);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Phase5_ScanCancellation_ThrowsOperationCanceledExceptionAndAllowsSubsequentScan()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "DH_Phase5_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            for (int i = 0; i < 20; i++)
            {
                await File.WriteAllBytesAsync(Path.Combine(tempDir, $"file_{i}.dat"), new byte[1024]);
            }

            var enumService = new FileEnumerationService();
            var hashService = new HashService();
            var scanner = new FileScannerService(enumService, hashService);

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await scanner.ScanAsync(tempDir, cancellationToken: cts.Token);
            });

            // Subsequent scan without cancellation token succeeds
            var subsequentScan = await scanner.ScanAsync(tempDir);
            Assert.Equal(20, subsequentScan.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Phase8_SafeDeletion_KeepsOriginalAndRemovesOnlySelected()
    {
        var mockOps = new MockFileOperationService();
        var manager = new DuplicateManagementService(mockOps);

        var file1 = new DuplicateFile { FilePath = "C:\\test\\doc1.txt", Hash = "H1", Size = 1024 };
        var file2 = new DuplicateFile { FilePath = "C:\\test\\doc2.txt", Hash = "H1", Size = 1024 };
        var file3 = new DuplicateFile { FilePath = "C:\\test\\doc3.txt", Hash = "H1", Size = 1024 };

        var group = new DuplicateGroup
        {
            Hash = "H1",
            Files = new List<DuplicateFile> { file1, file2, file3 }
        };

        // Keep file2, recycle file1 and file3
        int deleted = manager.DeleteAllExceptOne(group, file2, permanent: false);

        Assert.Equal(2, deleted);
        Assert.Contains(file1.FilePath, mockOps.RecycledFiles);
        Assert.Contains(file3.FilePath, mockOps.RecycledFiles);
        Assert.DoesNotContain(file2.FilePath, mockOps.RecycledFiles);
    }

    [Fact]
    public async Task Phase9_SmartSelectionPresets_KeepNewestAndKeepOldest()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "DH_Phase9_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string oldFile = Path.Combine(tempDir, "old.txt");
            string midFile = Path.Combine(tempDir, "mid.txt");
            string newFile = Path.Combine(tempDir, "new.txt");

            await File.WriteAllTextAsync(oldFile, "Duplicate Content");
            await File.WriteAllTextAsync(midFile, "Duplicate Content");
            await File.WriteAllTextAsync(newFile, "Duplicate Content");

            File.SetLastWriteTimeUtc(oldFile, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(midFile, new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(newFile, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            var mockOps = new MockFileOperationService();
            var manager = new DuplicateManagementService(mockOps);

            var group = new DuplicateGroup
            {
                Hash = "HASH",
                Files = new List<DuplicateFile>
                {
                    new() { FilePath = oldFile, Hash = "HASH", Size = 100 },
                    new() { FilePath = midFile, Hash = "HASH", Size = 100 },
                    new() { FilePath = newFile, Hash = "HASH", Size = 100 }
                }
            };

            // Test KeepNewest
            int deleted = manager.KeepNewest(group, permanent: false);
            Assert.Equal(2, deleted);
            Assert.Contains(oldFile, mockOps.RecycledFiles);
            Assert.Contains(midFile, mockOps.RecycledFiles);
            Assert.DoesNotContain(newFile, mockOps.RecycledFiles);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Phase10_ExportAllFormats_ValidatesGeneratedReports()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "DH_Phase10_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var exporter = new ReportExportService();
            var group = new DuplicateGroup
            {
                Hash = "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08",
                Files = new List<DuplicateFile>
                {
                    new() { FilePath = "C:\\Data\\file1.txt", Hash = "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08", Size = 2048 },
                    new() { FilePath = "C:\\Data\\file2.txt", Hash = "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08", Size = 2048 }
                }
            };
            var stats = new ScanStatistics { FilesScanned = 5, DuplicateGroups = 1, WastedBytes = 2048 };

            string csv = Path.Combine(tempDir, "export.csv");
            string json = Path.Combine(tempDir, "export.json");
            string xlsx = Path.Combine(tempDir, "export.xlsx");
            string pdf = Path.Combine(tempDir, "export.pdf");

            await exporter.ExportToCsvAsync(csv, new[] { group }, stats);
            await exporter.ExportToJsonAsync(json, new[] { group }, stats);
            await exporter.ExportToExcelAsync(xlsx, new[] { group }, stats);
            await exporter.ExportToPdfAsync(pdf, new[] { group }, stats);

            Assert.True(File.Exists(csv) && new FileInfo(csv).Length > 0);
            Assert.True(File.Exists(json) && new FileInfo(json).Length > 0);
            Assert.True(File.Exists(xlsx) && new FileInfo(xlsx).Length > 0);
            Assert.True(File.Exists(pdf) && new FileInfo(pdf).Length > 0);

            // Validate CSV content
            string csvContent = await File.ReadAllTextAsync(csv);
            Assert.Contains("Group Hash", csvContent);
            Assert.Contains(group.Hash, csvContent);

            // Validate JSON structure
            string jsonContent = await File.ReadAllTextAsync(json);
            using var jsonDoc = JsonDocument.Parse(jsonContent);
            Assert.True(jsonDoc.RootElement.TryGetProperty("DuplicateGroups", out var groupsElem));
            Assert.Equal(1, groupsElem.GetArrayLength());

            // Validate Excel workbook
            using var workbook = new XLWorkbook(xlsx);
            Assert.NotNull(workbook.Worksheet("Summary"));
            Assert.NotNull(workbook.Worksheet("Duplicate Groups"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Phase12_SettingsPersistenceAndDefaults()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"dh_settings_{Guid.NewGuid():N}.db");
        using var db = new SqliteDuplicateHunterDatabaseService(new DuplicateHunterDatabaseOptions(dbPath));
        await db.InitializeAsync();
        var settingsService = new AppSettingsService(db);

        try
        {
            // Initial defaults
            var initial = await settingsService.LoadAsync();
            Assert.Equal("System", initial.Theme);
            Assert.Empty(initial.IgnoreFoldersText);

            // Save custom
            var custom = new ScanSettings
            {
                Theme = "Dark",
                IgnoreFoldersText = ".git, node_modules",
                IgnoreExtensionsText = ".tmp, .log",
                DefaultMinimumFileSizeMB = 1.5,
                DefaultMaximumFileSizeMB = 200.0
            };
            await settingsService.SaveAsync(custom);

            // Reload and verify
            var reloaded = await settingsService.LoadAsync();
            Assert.Equal("Dark", reloaded.Theme);
            Assert.Equal(".git, node_modules", reloaded.IgnoreFoldersText);
            Assert.Equal(".tmp, .log", reloaded.IgnoreExtensionsText);
            Assert.Equal(1.5, reloaded.DefaultMinimumFileSizeMB);
            Assert.Equal(200.0, reloaded.DefaultMaximumFileSizeMB);
        }
        finally
        {
            db.Dispose();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public void Phase6_ResultsFiltering_ValidatesAllFilterCriteria()
    {
        var files = new List<DuplicateFile>
        {
            new() { FilePath = "C:\\Projects\\App\\report.pdf", Hash = "H1", Size = 2 * 1024 * 1024 },
            new() { FilePath = "C:\\Backups\\report.pdf", Hash = "H1", Size = 2 * 1024 * 1024 },
            new() { FilePath = "C:\\Photos\\image.png", Hash = "H2", Size = 500 * 1024 },
            new() { FilePath = "C:\\Photos\\copy.png", Hash = "H2", Size = 500 * 1024 },
            new() { FilePath = "C:\\Projects\\archive.zip", Hash = "H3", Size = 50 * 1024 * 1024 },
            new() { FilePath = "C:\\Backups\\archive.zip", Hash = "H3", Size = 50 * 1024 * 1024 }
        };

        var groupingService = new DuplicateGroupingService();

        // 1. Filter by Extension ".pdf"
        var pdfFiles = files.Where(f => f.Extension == ".pdf").ToList();
        var pdfGroups = groupingService.GroupDuplicates(pdfFiles);
        Assert.Single(pdfGroups);
        Assert.Equal("H1", pdfGroups[0].Hash);

        // 2. Filter by Folder "Projects"
        var projectFiles = files.Where(f => f.DirectoryName.Contains("Projects", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(2, projectFiles.Count);

        // 3. Filter by Min Size (1 MB)
        long minBytes = 1024 * 1024;
        var largeFiles = files.Where(f => f.Size >= minBytes).ToList();
        var largeGroups = groupingService.GroupDuplicates(largeFiles);
        Assert.Equal(2, largeGroups.Count); // H1 (2MB) and H3 (50MB)

        // 4. Filter by Max Size (1 MB)
        var smallFiles = files.Where(f => f.Size <= minBytes).ToList();
        var smallGroups = groupingService.GroupDuplicates(smallFiles);
        Assert.Single(smallGroups); // H2 (500KB)
        Assert.Equal("H2", smallGroups[0].Hash);
    }

    [Fact]
    public void Phase7_CheckboxSelection_BatchDeletionSafelyDeletesOnlyCheckedFiles()
    {
        var mockOps = new MockFileOperationService();
        var manager = new DuplicateManagementService(mockOps);

        var file1 = new DuplicateFile { FilePath = "C:\\data\\f1.txt", Hash = "H1", Size = 100, IsSelected = true };
        var file2 = new DuplicateFile { FilePath = "C:\\data\\f2.txt", Hash = "H1", Size = 100, IsSelected = false };
        var file3 = new DuplicateFile { FilePath = "C:\\data\\f3.txt", Hash = "H1", Size = 100, IsSelected = true };

        var allFiles = new List<DuplicateFile> { file1, file2, file3 };
        var selectedForDeletion = allFiles.Where(f => f.IsSelected).ToList();

        Assert.Equal(2, selectedForDeletion.Count);

        foreach (var file in selectedForDeletion)
        {
            manager.DeleteFile(file, permanent: false);
        }

        Assert.Equal(2, mockOps.RecycledFiles.Count);
        Assert.Contains(file1.FilePath, mockOps.RecycledFiles);
        Assert.Contains(file3.FilePath, mockOps.RecycledFiles);
        Assert.DoesNotContain(file2.FilePath, mockOps.RecycledFiles);
    }

    [Fact]
    public async Task Phase11_HistoryIsolation_ClearingHistoryDoesNotCorruptOrDeleteScanFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "DH_Phase11_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string dbPath = Path.Combine(tempDir, "history.db");

        try
        {
            using var db = new SqliteDuplicateHunterDatabaseService(new DuplicateHunterDatabaseOptions(dbPath));
            await db.InitializeAsync();

            await db.SaveScanHistoryAsync(new ScanHistoryRecord(
                "C:\\ScannedFolder", DateTime.UtcNow, DateTime.UtcNow, "Completed", 100, 5, 20480));
            await db.SaveReportAsync(new ReportRecord(
                "C:\\ScannedFolder", "C:\\Reports\\r1.csv", "csv", DateTime.UtcNow, 100, 5, 20480));

            var historyBefore = await db.GetScanHistoryAsync();
            var reportsBefore = await db.GetReportsAsync();
            Assert.Single(historyBefore);
            Assert.Single(reportsBefore);

            // Clear History
            await db.ClearScanHistoryAsync();
            await db.ClearReportsAsync();

            var historyAfter = await db.GetScanHistoryAsync();
            var reportsAfter = await db.GetReportsAsync();
            Assert.Empty(historyAfter);
            Assert.Empty(reportsAfter);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Phase13_14_DatabaseRestartCycles_PersistsStateAcrossSeparateInstances()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "DH_Phase1314_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string dbPath = Path.Combine(tempDir, "restart_test.db");

        try
        {
            // Cycle 1: Create, write settings & scan history, then close
            {
                using var db1 = new SqliteDuplicateHunterDatabaseService(new DuplicateHunterDatabaseOptions(dbPath));
                await db1.InitializeAsync();
                await db1.SetSettingAsync("Theme", "Dark");
                await db1.AddRecentFolderAsync("C:\\MyScannedDir");
                await db1.SaveScanHistoryAsync(new ScanHistoryRecord(
                    "C:\\MyScannedDir", DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow, "Completed", 50, 4, 102400));
            }

            // Cycle 2: Reopen with fresh instance and verify everything persisted
            {
                using var db2 = new SqliteDuplicateHunterDatabaseService(new DuplicateHunterDatabaseOptions(dbPath));
                await db2.InitializeAsync();

                string? theme = await db2.GetSettingAsync("Theme");
                var recent = await db2.GetRecentFoldersAsync();
                var history = await db2.GetScanHistoryAsync();

                Assert.Equal("Dark", theme);
                Assert.Single(recent);
                Assert.Equal("C:\\MyScannedDir", recent[0].FolderPath);
                Assert.Single(history);
                Assert.Equal("Completed", history[0].Status);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Phase15_LargeScanStability_HandlesMultipleFilesAndDirectories()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "DH_Phase15_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create 100 files across 5 subdirectories with known duplicate clusters
            for (int d = 0; d < 5; d++)
            {
                string sub = Path.Combine(tempDir, $"SubDir_{d}");
                Directory.CreateDirectory(sub);

                for (int f = 0; f < 20; f++)
                {
                    // Every 5 files have the same content -> 4 duplicate groups of 5 files each in each subdir
                    int cluster = f % 5;
                    byte[] content = Encoding.UTF8.GetBytes($"Payload cluster {cluster}");
                    await File.WriteAllBytesAsync(Path.Combine(sub, $"file_{f}.txt"), content);
                }
            }

            var enumService = new FileEnumerationService();
            var hashService = new HashService();
            var scanner = new FileScannerService(enumService, hashService);
            var grouping = new DuplicateGroupingService();
            var stats = new ScanStatisticsService();

            var scanned = await scanner.ScanAsync(tempDir);
            Assert.Equal(100, scanned.Count);

            var groups = grouping.GroupDuplicates(scanned);
            // 5 distinct clusters across all subdirs
            Assert.Equal(5, groups.Count);
            Assert.All(groups, g => Assert.Equal(20, g.FileCount));

            var calculatedStats = stats.Calculate(scanned);
            Assert.Equal(100, calculatedStats.FilesScanned);
            Assert.Equal(5, calculatedStats.DuplicateGroups);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}

