namespace FlexGuard.Core.Reporting;

public class MessageReporterWithProgress : IMessageReporter
{
    private readonly IMessageReporter _base;
    private readonly Action<long, long, string> _progressCallback;
    private long _currentBytes = 0;
    private long _totalBytes;

    public MessageReporterWithProgress(IMessageReporter baseReporter, long totalBytes, Action<long, long, string> progressCallback)
    {
        _base = baseReporter;
        _progressCallback = progressCallback;
        _totalBytes = totalBytes;
    }

    public void ReportProgress(string filename)
    {
        _progressCallback(_currentBytes, _totalBytes, filename);
    }
    public void ReportProgress(long fileSize, string filename)
    {
        _currentBytes += fileSize;
        _progressCallback(_currentBytes, _totalBytes, filename);
    }

    // Delegér alle andre kald
    public void Info(string message) => _base.Info(message);
    public void Verbose(string message) => _base.Verbose(message);
    public void Success(string message) => _base.Success(message);
    public void Warning(string message) => _base.Warning(message);
    public void Error(string message) => _base.Error(message);
    public void Error(Exception ex) => _base.Error(ex);
    public void Debug(string message) => _base.Debug(message);
    public void WriteRaw(string message) => _base.WriteRaw(message);
    public void ReportProgress(long currentBytes, long totalBytes, string file) =>
        _progressCallback(currentBytes, totalBytes, file); // optional passthrough
}