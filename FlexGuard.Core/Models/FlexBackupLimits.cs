namespace FlexGuard.Core.Models
{
    public static class FlexBackupLimits
    {
        public const int UlidLen = 26;
        public const int JobNameMax = 50;
        public const int DestinationBackupFolderMax = 255;
        public const int StatusMessageMax = 255;
        public const int ChunkFileNameMax = 50;
        public const int RelativePathMax = 512;
        public const int HashHexLen = 64;
    }
    public enum RunStatus
    {
        Running = 0,
        Completed = 1,
        Failed = 2,
        Canceled = 3
    }
}