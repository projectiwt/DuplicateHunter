using DuplicateHunter.Services;
using Xunit;

namespace DuplicateHunter.Tests;

public class FileEnumerationServiceTests
{
    [Fact]
    public void GetFiles_ReturnsEmptyForNonExistentDirectory()
    {
        var service = new FileEnumerationService();
        string nonExistentPath = Path.Combine(Path.GetTempPath(), $"dh_nonexistent_{Guid.NewGuid():N}");

        var files = service.GetFiles(nonExistentPath);

        Assert.Empty(files);
    }

    [Fact]
    public void GetFiles_EnumeratesAllAccessibleFilesInSubdirectories()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "DuplicateHunter.Tests", $"enum_test_{Guid.NewGuid():N}");
        string subDir1 = Path.Combine(tempRoot, "Sub1");
        string subDir2 = Path.Combine(tempRoot, "Sub2", "Nested");

        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);

        string file1 = Path.Combine(tempRoot, "root_file.txt");
        string file2 = Path.Combine(subDir1, "sub1_file.pdf");
        string file3 = Path.Combine(subDir2, "nested_file.bin");

        File.WriteAllText(file1, "Root");
        File.WriteAllText(file2, "Sub1");
        File.WriteAllText(file3, "Nested");

        var service = new FileEnumerationService();

        try
        {
            var files = service.GetFiles(tempRoot);

            Assert.Equal(3, files.Count);
            Assert.Contains(file1, files);
            Assert.Contains(file2, files);
            Assert.Contains(file3, files);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void GetFiles_HandlesEmptyDirectory()
    {
        string emptyDir = Path.Combine(Path.GetTempPath(), "DuplicateHunter.Tests", $"empty_{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyDir);

        var service = new FileEnumerationService();

        try
        {
            var files = service.GetFiles(emptyDir);
            Assert.Empty(files);
        }
        finally
        {
            if (Directory.Exists(emptyDir))
                Directory.Delete(emptyDir, recursive: true);
        }
    }
}
