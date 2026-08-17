using DuplicateHunter.Database;
using DuplicateHunter.Models;
using DuplicateHunter.Services;
using Xunit;

namespace DuplicateHunter.Tests;

public class SelectionSynchronizationTests
{
    [Fact]
    public void DuplicateGroupingAndManagement_HandlesSelectionOperationsWithoutExceptions()
    {
        // Arrange
        var groupingService = new DuplicateGroupingService();
        var files = new List<DuplicateFile>
        {
            new() { FilePath = "C:\\test\\f1.txt", Hash = "H1", Size = 100 },
            new() { FilePath = "C:\\test\\f2.txt", Hash = "H1", Size = 100 },
            new() { FilePath = "C:\\test\\f3.jpg", Hash = "H2", Size = 500 },
            new() { FilePath = "C:\\test\\f4.jpg", Hash = "H2", Size = 500 }
        };

        var groups = groupingService.GroupDuplicates(files);

        // Act & Assert
        Assert.Equal(2, groups.Count);
        Assert.Equal(".txt", files[0].Extension);
        Assert.Equal("C:\\test", files[0].DirectoryName);

        // Simulate selection toggles
        foreach (var file in files)
        {
            file.IsSelected = true;
            Assert.True(file.IsSelected);
            file.IsSelected = false;
            Assert.False(file.IsSelected);
        }
    }
}
