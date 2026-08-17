using DuplicateHunter.Models;
using DuplicateHunter.Services;
using Xunit;

namespace DuplicateHunter.Tests;

public class DuplicateGroupingServiceTests
{
    [Fact]
    public void GroupDuplicates_CorrectlyClustersIdenticalHashes()
    {
        // Arrange
        var service = new DuplicateGroupingService();

        var file1 = new DuplicateFile { FilePath = "C:\\a\\pic1.jpg", Hash = "HASH_A", Size = 1000 };
        var file2 = new DuplicateFile { FilePath = "C:\\b\\pic2.jpg", Hash = "HASH_A", Size = 1000 };
        var file3 = new DuplicateFile { FilePath = "C:\\c\\doc1.pdf", Hash = "HASH_B", Size = 5000 };
        var file4 = new DuplicateFile { FilePath = "C:\\d\\doc2.pdf", Hash = "HASH_B", Size = 5000 };
        var file5 = new DuplicateFile { FilePath = "C:\\e\\doc3.pdf", Hash = "HASH_B", Size = 5000 };
        var singleton = new DuplicateFile { FilePath = "C:\\f\\unique.txt", Hash = "HASH_UNIQUE", Size = 200 };

        var files = new List<DuplicateFile> { file1, file2, file3, file4, file5, singleton };

        // Act
        var groups = service.GroupDuplicates(files);

        // Assert
        Assert.Equal(2, groups.Count);

        // Group with most files should be first
        Assert.Equal("HASH_B", groups[0].Hash);
        Assert.Equal(3, groups[0].FileCount);
        Assert.Equal(15000, groups[0].TotalSize);

        // Second group
        Assert.Equal("HASH_A", groups[1].Hash);
        Assert.Equal(2, groups[1].FileCount);
        Assert.Equal(2000, groups[1].TotalSize);
    }

    [Fact]
    public void GroupDuplicates_ReturnsEmptyListWhenNoDuplicatesExist()
    {
        // Arrange
        var service = new DuplicateGroupingService();

        var file1 = new DuplicateFile { FilePath = "C:\\a\\file1.txt", Hash = "HASH_1", Size = 100 };
        var file2 = new DuplicateFile { FilePath = "C:\\b\\file2.txt", Hash = "HASH_2", Size = 200 };
        var file3 = new DuplicateFile { FilePath = "C:\\c\\file3.txt", Hash = "HASH_3", Size = 300 };

        var files = new List<DuplicateFile> { file1, file2, file3 };

        // Act
        var groups = service.GroupDuplicates(files);

        // Assert
        Assert.Empty(groups);
    }

    [Fact]
    public void DuplicateGroup_FormattedSize_CalculatesUnitsCorrectly()
    {
        // Arrange
        var groupKB = new DuplicateGroup
        {
            Hash = "H1",
            Files = new List<DuplicateFile>
            {
                new() { FilePath = "C:\\f1", Size = 1024 * 50 },
                new() { FilePath = "C:\\f2", Size = 1024 * 50 }
            }
        };

        var groupMB = new DuplicateGroup
        {
            Hash = "H2",
            Files = new List<DuplicateFile>
            {
                new() { FilePath = "C:\\f3", Size = 1024 * 1024 * 10 },
                new() { FilePath = "C:\\f4", Size = 1024 * 1024 * 10 }
            }
        };

        // Assert
        Assert.Equal("100.00 KB", groupKB.FormattedSize);
        Assert.Equal("20.00 MB", groupMB.FormattedSize);
    }
}
