using DuplicateHunter.Models;
using DuplicateHunter.Services;
using Xunit;

namespace DuplicateHunter.Tests;

public class ScanStatisticsServiceTests
{
    [Fact]
    public void Calculate_AccuratelyComputesStatisticsAndWastedStorage()
    {
        // Arrange
        var service = new ScanStatisticsService();

        // 3 files in Group A of size 1 MB each -> 2 MB wasted
        var fileA1 = new DuplicateFile { FilePath = "C:\\a1.bin", Hash = "HASH_A", Size = 1024 * 1024 };
        var fileA2 = new DuplicateFile { FilePath = "C:\\a2.bin", Hash = "HASH_A", Size = 1024 * 1024 };
        var fileA3 = new DuplicateFile { FilePath = "C:\\a3.bin", Hash = "HASH_A", Size = 1024 * 1024 };

        // 2 files in Group B of size 500 KB each -> 500 KB wasted
        var fileB1 = new DuplicateFile { FilePath = "C:\\b1.bin", Hash = "HASH_B", Size = 512 * 1024 };
        var fileB2 = new DuplicateFile { FilePath = "C:\\b2.bin", Hash = "HASH_B", Size = 512 * 1024 };

        // 1 unique file
        var fileC1 = new DuplicateFile { FilePath = "C:\\c1.bin", Hash = "HASH_C", Size = 100 };

        var files = new List<DuplicateFile> { fileA1, fileA2, fileA3, fileB1, fileB2, fileC1 };

        // Act
        var stats = service.Calculate(files);

        // Assert
        Assert.Equal(6, stats.FilesScanned);
        Assert.Equal(2, stats.DuplicateGroups);

        long expectedWastedBytes = (2 * 1024 * 1024) + (1 * 512 * 1024);
        Assert.Equal(expectedWastedBytes, stats.WastedBytes);
        Assert.Equal("2.50 MB", stats.WastedSpace);
    }

    [Fact]
    public void Calculate_ReturnsZeroForEmptyFileList()
    {
        // Arrange
        var service = new ScanStatisticsService();

        // Act
        var stats = service.Calculate(new List<DuplicateFile>());

        // Assert
        Assert.Equal(0, stats.FilesScanned);
        Assert.Equal(0, stats.DuplicateGroups);
        Assert.Equal(0, stats.WastedBytes);
        Assert.Equal("0.00 MB", stats.WastedSpace);
    }
}
