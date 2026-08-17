using DuplicateHunter.Models;
using DuplicateHunter.Services;
using Xunit;

namespace DuplicateHunter.Tests;

public class DuplicateManagementServiceTests
{
    private class FakeFileOperationService : IFileOperationService
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
            return true;
        }

        public bool DeletePermanently(string filePath)
        {
            PermanentlyDeletedFiles.Add(filePath);
            return true;
        }
    }

    [Fact]
    public void DeleteAllExceptOne_RecyclesAllOtherFiles()
    {
        // Arrange
        var fakeFileOp = new FakeFileOperationService();
        var service = new DuplicateManagementService(fakeFileOp);

        var file1 = new DuplicateFile { FilePath = "C:\\test\\file1.txt", Hash = "HASH123", Size = 100 };
        var file2 = new DuplicateFile { FilePath = "C:\\test\\file2.txt", Hash = "HASH123", Size = 100 };
        var file3 = new DuplicateFile { FilePath = "C:\\test\\file3.txt", Hash = "HASH123", Size = 100 };

        var group = new DuplicateGroup
        {
            Hash = "HASH123",
            Files = new List<DuplicateFile> { file1, file2, file3 }
        };

        // Act
        int count = service.DeleteAllExceptOne(group, file2, permanent: false);

        // Assert
        Assert.Equal(2, count);
        Assert.Contains(file1.FilePath, fakeFileOp.RecycledFiles);
        Assert.Contains(file3.FilePath, fakeFileOp.RecycledFiles);
        Assert.DoesNotContain(file2.FilePath, fakeFileOp.RecycledFiles);
    }

    [Fact]
    public void KeepLargest_PreservesLargestFile()
    {
        // Arrange
        var fakeFileOp = new FakeFileOperationService();
        var service = new DuplicateManagementService(fakeFileOp);

        var fileSmall = new DuplicateFile { FilePath = "C:\\test\\small.txt", Hash = "HASH123", Size = 50 };
        var fileLarge = new DuplicateFile { FilePath = "C:\\test\\large.txt", Hash = "HASH123", Size = 500 };

        var group = new DuplicateGroup
        {
            Hash = "HASH123",
            Files = new List<DuplicateFile> { fileSmall, fileLarge }
        };

        // Act
        int count = service.KeepLargest(group, permanent: false);

        // Assert
        Assert.Equal(1, count);
        Assert.Contains(fileSmall.FilePath, fakeFileOp.RecycledFiles);
        Assert.DoesNotContain(fileLarge.FilePath, fakeFileOp.RecycledFiles);
    }

    [Fact]
    public void DeleteAllExceptOne_IgnoresKeepFileWhenItIsNotInGroup()
    {
        // Arrange
        var fakeFileOp = new FakeFileOperationService();
        var service = new DuplicateManagementService(fakeFileOp);

        var file1 = new DuplicateFile { FilePath = "C:\\test\\file1.txt", Hash = "HASH123", Size = 100 };
        var file2 = new DuplicateFile { FilePath = "C:\\test\\file2.txt", Hash = "HASH123", Size = 100 };
        var keepFile = new DuplicateFile { FilePath = "C:\\test\\missing.txt", Hash = "HASH123", Size = 100 };

        var group = new DuplicateGroup
        {
            Hash = "HASH123",
            Files = new List<DuplicateFile> { file1, file2 }
        };

        // Act
        int count = service.DeleteAllExceptOne(group, keepFile, permanent: false);

        // Assert
        Assert.Equal(0, count);
        Assert.Empty(fakeFileOp.RecycledFiles);
    }

    [Fact]
    public void KeepSmallest_PreservesSmallestFile()
    {
        // Arrange
        var fakeFileOp = new FakeFileOperationService();
        var service = new DuplicateManagementService(fakeFileOp);

        var fileSmall = new DuplicateFile { FilePath = "C:\\test\\small.txt", Hash = "HASH123", Size = 50 };
        var fileLarge = new DuplicateFile { FilePath = "C:\\test\\large.txt", Hash = "HASH123", Size = 500 };

        var group = new DuplicateGroup
        {
            Hash = "HASH123",
            Files = new List<DuplicateFile> { fileSmall, fileLarge }
        };

        // Act
        int count = service.KeepSmallest(group, permanent: false);

        // Assert
        Assert.Equal(1, count);
        Assert.Contains(fileLarge.FilePath, fakeFileOp.RecycledFiles);
        Assert.DoesNotContain(fileSmall.FilePath, fakeFileOp.RecycledFiles);
    }

    [Fact]
    public void DeleteFile_Permanent_UsesDeletePermanently()
    {
        // Arrange
        var fakeFileOp = new FakeFileOperationService();
        var service = new DuplicateManagementService(fakeFileOp);
        var file = new DuplicateFile { FilePath = "C:\\test\\perm.txt", Hash = "H1", Size = 100 };

        // Act
        bool result = service.DeleteFile(file, permanent: true);

        // Assert
        Assert.True(result);
        Assert.Contains(file.FilePath, fakeFileOp.PermanentlyDeletedFiles);
        Assert.DoesNotContain(file.FilePath, fakeFileOp.RecycledFiles);
    }

    [Fact]
    public void DeleteFile_Recycle_UsesMoveToRecycleBin()
    {
        // Arrange
        var fakeFileOp = new FakeFileOperationService();
        var service = new DuplicateManagementService(fakeFileOp);
        var file = new DuplicateFile { FilePath = "C:\\test\\recycle.txt", Hash = "H1", Size = 100 };

        // Act
        bool result = service.DeleteFile(file, permanent: false);

        // Assert
        Assert.True(result);
        Assert.Contains(file.FilePath, fakeFileOp.RecycledFiles);
        Assert.DoesNotContain(file.FilePath, fakeFileOp.PermanentlyDeletedFiles);
    }
}

