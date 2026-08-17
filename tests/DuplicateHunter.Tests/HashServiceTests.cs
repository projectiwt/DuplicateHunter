using System.Security.Cryptography;
using System.Text;
using DuplicateHunter.Services;
using Xunit;

namespace DuplicateHunter.Tests;

public class HashServiceTests
{
    [Fact]
    public async Task ComputeHashAsync_CalculatesAccurateSha256()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"dh_hash_test_{Guid.NewGuid():N}.txt");
        string sampleText = "DuplicateHunter SHA-256 Test Content 12345";
        byte[] expectedBytes = Encoding.UTF8.GetBytes(sampleText);
        await File.WriteAllBytesAsync(tempFile, expectedBytes);

        byte[] expectedHashBytes = SHA256.HashData(expectedBytes);
        string expectedHex = Convert.ToHexString(expectedHashBytes);

        var service = new HashService();

        try
        {
            // Act
            string actualHash = await service.ComputeHashAsync(tempFile);

            // Assert
            Assert.Equal(expectedHex, actualHash);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ComputeHashAsync_ProducesIdenticalHashesForIdenticalFiles()
    {
        // Arrange
        string file1 = Path.Combine(Path.GetTempPath(), $"dh_hash_1_{Guid.NewGuid():N}.txt");
        string file2 = Path.Combine(Path.GetTempPath(), $"dh_hash_2_{Guid.NewGuid():N}.txt");

        byte[] data = Encoding.UTF8.GetBytes("Exact same binary payload for duplicate test.");
        await File.WriteAllBytesAsync(file1, data);
        await File.WriteAllBytesAsync(file2, data);

        var service = new HashService();

        try
        {
            // Act
            string hash1 = await service.ComputeHashAsync(file1);
            string hash2 = await service.ComputeHashAsync(file2);

            // Assert
            Assert.Equal(hash1, hash2);
        }
        finally
        {
            if (File.Exists(file1)) File.Delete(file1);
            if (File.Exists(file2)) File.Delete(file2);
        }
    }

    [Fact]
    public async Task ComputeHashAsync_ProducesDifferentHashesForDifferentFiles()
    {
        // Arrange
        string file1 = Path.Combine(Path.GetTempPath(), $"dh_diff_1_{Guid.NewGuid():N}.txt");
        string file2 = Path.Combine(Path.GetTempPath(), $"dh_diff_2_{Guid.NewGuid():N}.txt");

        await File.WriteAllTextAsync(file1, "File A Content");
        await File.WriteAllTextAsync(file2, "File B Content");

        var service = new HashService();

        try
        {
            // Act
            string hash1 = await service.ComputeHashAsync(file1);
            string hash2 = await service.ComputeHashAsync(file2);

            // Assert
            Assert.NotEqual(hash1, hash2);
        }
        finally
        {
            if (File.Exists(file1)) File.Delete(file1);
            if (File.Exists(file2)) File.Delete(file2);
        }
    }

    [Fact]
    public async Task ComputeHashAsync_HandlesEmptyFile()
    {
        // Arrange
        string emptyFile = Path.Combine(Path.GetTempPath(), $"dh_empty_{Guid.NewGuid():N}.txt");
        await File.WriteAllBytesAsync(emptyFile, Array.Empty<byte>());

        string emptySha256 = Convert.ToHexString(SHA256.HashData(Array.Empty<byte>()));
        var service = new HashService();

        try
        {
            // Act
            string hash = await service.ComputeHashAsync(emptyFile);

            // Assert
            Assert.Equal(emptySha256, hash);
        }
        finally
        {
            if (File.Exists(emptyFile)) File.Delete(emptyFile);
        }
    }
}
