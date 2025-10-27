namespace FlexGuard.Core.Models
{
    public static class FlexBackupLimits
    {
        public const int JobNameMax = 50;
        public const int StatusMessageMax = 255;
        public const int ChunkFileNameMax = 50;
        public const int RelativePathMax = 255;
        public const int HashHexLen = 64;
    }
    public enum OperationMode
    {
        Unknown = 0,
        Backup = 1,
        Restore = 2,
        Verify = 3
    }
    // Reuses CompressionMethod from NewFileManifest.cs:
    // public enum CompressionMethod { None, GZip, Brotli, Zstd }
    public enum RunStatus
    {
        Running = 0,
        Completed = 1,
        Failed = 2,
        Canceled = 3
    }
}