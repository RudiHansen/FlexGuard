namespace FlexGuard.Core.Reporting
{
    public interface IMessageReporter
    {
        void Info(string message);
        void Verbose(string message);
        void Success(string message);
        void Warning(string message);
        void Error(string message);
        void Error(Exception ex);
        void Debug(string message);
        void WriteRaw(string message);

        void ReportProgress(long currentBytes, long totalBytes, string file);
        void ReportProgress(long fileSize, string filename);
    }
}
