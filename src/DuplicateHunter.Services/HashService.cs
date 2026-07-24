using System.Security.Cryptography;

namespace DuplicateHunter.Services;

public class HashService
{
    public async Task<string> ComputeHashAsync(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);

        using SHA256 sha256 = SHA256.Create();

        byte[] hash = await sha256.ComputeHashAsync(stream);

        return Convert.ToHexString(hash);
    }
}