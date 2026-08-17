using System.IO;
using System.IO.Compression;
using System.Text.Json;
using DuplicateHunter.Models;
using DuplicateHunter.Services;
using Xunit;

namespace DuplicateHunter.Tests;

public class ReportExportServiceTests
{
    [Fact]
    public async Task ExportToCsvAsync_CreatesValidCsvFile()
    {
        // Arrange
        var service = new ReportExportService();
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_report_{Guid.NewGuid()}.csv");

        var file1 = new DuplicateFile { FilePath = "C:\\test\\file1.txt", Hash = "HASH1", Size = 2048 };
        var file2 = new DuplicateFile { FilePath = "C:\\test\\file2.txt", Hash = "HASH1", Size = 2048 };

        var group = new DuplicateGroup
        {
            Hash = "HASH1",
            Files = new List<DuplicateFile> { file1, file2 }
        };

        var stats = new ScanStatistics { FilesScanned = 10, DuplicateGroups = 1, WastedBytes = 2048 };

        try
        {
            // Act
            await service.ExportToCsvAsync(tempPath, new[] { group }, stats);

            // Assert
            Assert.True(File.Exists(tempPath));
            string content = await File.ReadAllTextAsync(tempPath);
            Assert.Contains("Group Hash,File Name,File Path,Extension,Size (Bytes),Formatted Size", content);
            Assert.Contains("HASH1", content);
            Assert.Contains("file1.txt", content);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ExportToJsonAsync_CreatesValidJsonFile()
    {
        // Arrange
        var service = new ReportExportService();
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_report_{Guid.NewGuid()}.json");

        var file1 = new DuplicateFile { FilePath = "C:\\test\\file1.txt", Hash = "HASH1", Size = 1000 };
        var file2 = new DuplicateFile { FilePath = "C:\\test\\file2.txt", Hash = "HASH1", Size = 1000 };

        var group = new DuplicateGroup
        {
            Hash = "HASH1",
            Files = new List<DuplicateFile> { file1, file2 }
        };

        var stats = new ScanStatistics { FilesScanned = 5, DuplicateGroups = 1, WastedBytes = 1000 };

        try
        {
            // Act
            await service.ExportToJsonAsync(tempPath, new[] { group }, stats);

            // Assert
            Assert.True(File.Exists(tempPath));
            string content = await File.ReadAllTextAsync(tempPath);
            using var doc = JsonDocument.Parse(content);
            Assert.True(doc.RootElement.TryGetProperty("ExportDate", out _));
            Assert.True(doc.RootElement.TryGetProperty("DuplicateGroups", out var groupsElem));
            Assert.Equal(1, groupsElem.GetArrayLength());
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ExportToExcelAsync_CreatesValidXlsxFile()
    {
        // Arrange
        var service = new ReportExportService();
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_report_{Guid.NewGuid()}.xlsx");

        var file1 = new DuplicateFile { FilePath = "C:\\test\\file1.txt", Hash = "HASH1", Size = 1000 };
        var file2 = new DuplicateFile { FilePath = "C:\\test\\file2.txt", Hash = "HASH1", Size = 1000 };

        var group = new DuplicateGroup
        {
            Hash = "HASH1",
            Files = new List<DuplicateFile> { file1, file2 }
        };

        var stats = new ScanStatistics { FilesScanned = 5, DuplicateGroups = 1, WastedBytes = 1000 };

        try
        {
            // Act
            await service.ExportToExcelAsync(tempPath, new[] { group }, stats);

            // Assert
            Assert.True(File.Exists(tempPath));

            using var archive = ZipFile.OpenRead(tempPath);
            Assert.Contains(archive.Entries, entry => entry.FullName == "[Content_Types].xml");
            Assert.Contains(archive.Entries, entry => entry.FullName == "xl/workbook.xml");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ExportToPdfAsync_CreatesValidPdfFile()
    {
        // Arrange
        var service = new ReportExportService();
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_report_{Guid.NewGuid()}.pdf");

        var file1 = new DuplicateFile { FilePath = "C:\\test\\file1.txt", Hash = "HASH1", Size = 1000 };
        var file2 = new DuplicateFile { FilePath = "C:\\test\\file2.txt", Hash = "HASH1", Size = 1000 };

        var group = new DuplicateGroup
        {
            Hash = "HASH1",
            Files = new List<DuplicateFile> { file1, file2 }
        };

        var stats = new ScanStatistics { FilesScanned = 5, DuplicateGroups = 1, WastedBytes = 1000 };

        try
        {
            // Act
            await service.ExportToPdfAsync(tempPath, new[] { group }, stats);

            // Assert
            Assert.True(File.Exists(tempPath));

            byte[] header = await File.ReadAllBytesAsync(tempPath);
            Assert.True(header.Length > 4);
            Assert.Equal('%', (char)header[0]);
            Assert.Equal('P', (char)header[1]);
            Assert.Equal('D', (char)header[2]);
            Assert.Equal('F', (char)header[3]);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
