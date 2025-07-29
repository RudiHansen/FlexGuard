using System.Security.Cryptography;

namespace FlexGuard.Core.Util;
public static class HashHelper
{
    public static string ComputeHash(string filePath, string algorithm = "SHA256")
    {
        return algorithm.ToUpperInvariant() switch
        {
            "SHA256" => ComputeSha256(filePath),
            _ => throw new NotSupportedException($"Unsupported hash algorithm: {algorithm}")
        };
    }
    public static string ComputeHash(Stream stream, string algorithm = "SHA256", bool resetPosition = true)
    {
        string result = algorithm.ToUpperInvariant() switch
        {
            "SHA256" => ComputeSha256(stream),
            _ => throw new NotSupportedException($"Unsupported hash algorithm: {algorithm}")
        };

        if (resetPosition && stream.CanSeek)
            stream.Position = 0;

        return result;
    }
    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return ComputeSha256(stream);
    }
    public static string ComputeSha256(Stream stream)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}