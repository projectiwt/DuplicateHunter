using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace DuplicateHunter.Services;

public class HashService
{
    public async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        using FileStream stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1024 * 64,
            options: FileOptions.SequentialScan | FileOptions.Asynchronous);

        using SHA256 sha256 = SHA256.Create();

        byte[] hash = await sha256.ComputeHashAsync(stream, cancellationToken);

        return Convert.ToHexString(hash);
    }
}