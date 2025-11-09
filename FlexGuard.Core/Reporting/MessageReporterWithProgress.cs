namespace FlexGuard.Core.Reporting;

public class MessageReporterWithProgress : IMessageReporter
{
    private readonly IMessageReporter _base;
    private readonly Action<long, long, string> _progressCallback;
    private long _currentBytes = 0;
    private readonly long _totalBytes;

    // beskytter selve callback'et, så flere tråde ikke skriver til progress samtidig
    private static readonly object _progressLock = new();

    public MessageReporterWithProgress(IMessageReporter baseReporter, long totalBytes, Action<long, long, string> progressCallback)
    {
        _base = baseReporter;
        _progressCallback = progressCallback;
        _totalBytes = totalBytes;
    }

    // kald fra fx "vi skifter bare filnavn"
    public void ReportProgress(string filename)
    {
        lock (_progressLock)
        {
            _progressCallback(_currentBytes, _totalBytes, filename);
        }
    }

    // kald fra "vi har lige skrevet X bytes af en fil"
    public void ReportProgress(long fileSize, string filename)
    {
        // trådsikker increment
        long current = Interlocked.Add(ref _currentBytes, fileSize);

        lock (_progressLock)
        {
            _progressCallback(current, _totalBytes, filename);
        }
    }

    // passthrough-varianten bruger vi også med lås
    public void ReportProgress(long currentBytes, long totalBytes, string file)
    {
        lock (_progressLock)
        {
            _progressCallback(currentBytes, totalBytes, file);
        }
    }

    // Delegér alle andre kald uændret
    public void Info(string message) => _base.Info(message);
    public void Verbose(string message) => _base.Verbose(message);
    public void Success(string message) => _base.Success(message);
    public void Warning(string message) => _base.Warning(message);
    public void Error(string message) => _base.Error(message);
    public void Error(Exception ex) => _base.Error(ex);
    public void Debug(string message) => _base.Debug(message);
    public void WriteRaw(string message) => _base.WriteRaw(message);
}