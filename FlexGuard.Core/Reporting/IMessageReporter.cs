namespace FlexGuard.Core.Reporting
{
    public interface IMessageReporter
    {
        void Info(string message);
        void Success(string message);
        void Warning(string message);
        void Error(string message);
        void Error(Exception ex);
        void Debug(string message);
        void WriteRaw(string message);

        void ReportProgress(int current, int total, string file);
    }
}
