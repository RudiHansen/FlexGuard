namespace FlexGuard.Core.Reporting;

public class MessageReporterWithProgress : IMessageReporter
{
    private readonly IMessageReporter _base;
    private readonly Action<int, int, string> _progressCallback;

    public MessageReporterWithProgress(IMessageReporter @base, Action<int, int, string> progressCallback)
    {
        _base = @base;
        _progressCallback = progressCallback;
    }

    public void Info(string message) => _base.Info(message);
    public void Success(string message) => _base.Success(message);
    public void Warning(string message) => _base.Warning(message);
    public void Error(string message) => _base.Error(message);
    public void Error(Exception ex) => _base.Error(ex);
    public void Debug(string message) => _base.Debug(message);
    public void WriteRaw(string message) => _base.WriteRaw(message);

    public void ReportProgress(int current, int total, string file) =>
        _progressCallback(current, total, file);
}