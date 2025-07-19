namespace FlexGuard.Core.Hashing;

public interface IHasher
{
    string ComputeHash(string filePath);
}