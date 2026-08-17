using DuplicateHunter.Models;
using DuplicateHunter.Services;
using Xunit;

namespace DuplicateHunter.Tests;

public class FileScannerServiceTests
{
    [Fact]
    public async Task ScanAsync_RespectsIgnoreExtensionsAndMinimumFileSizeSettings()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "DuplicateHunter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        string scanDirectory = Path.Combine(tempDirectory, "scan");
        Directory.CreateDirectory(scanDirectory);

        string includedFile = Path.Combine(scanDirectory, "included.txt");
        string ignoredExtensionFile = Path.Combine(scanDirectory, "ignored.tmp");
        string ignoredSmallFile = Path.Combine(scanDirectory, "small.txt");

        await File.WriteAllTextAsync(includedFile, new string('A', 4096));
        await File.WriteAllTextAsync(ignoredExtensionFile, new string('B', 4096));
        await File.WriteAllTextAsync(ignoredSmallFile, "x");

        var scanner = new FileScannerService(new FileEnumerationService(), new HashService());
        var settings = new ScanSettings
        {
            IgnoreExtensionsText = ".tmp",
            DefaultMinimumFileSizeMB = 0.001
        };

        try
        {
            var results = await scanner.ScanAsync(scanDirectory, cancellationToken: default, settings: settings);

            Assert.Single(results);
            Assert.Equal(includedFile, results[0].FilePath);
            Assert.Equal(4096, results[0].Size);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
