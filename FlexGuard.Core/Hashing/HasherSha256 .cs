using System.Security.Cryptography;
using System.Text;

namespace FlexGuard.Core.Hashing;

public class Sha256Hasher : IHasher
{
    public string ComputeHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes);
    }
}