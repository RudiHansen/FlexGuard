namespace FlexGuard.Core.Models;
public static class NewFileManifestLimits
{
    public const int JobNameMax = 50;
    public const int TypeMax = 20;          // hvis du ender med string i data/DB
    public const int PathMax = 1024;        // RelativePath, ChunkFile
    public const int HashHexLen = 64;       // SHA-256 som hex
}