using FlexGuard.Core.Config;
using FlexGuard.Core.Options;
using FlexGuard.Core.Reporting;
using FlexGuard.Core.Util;

class Program
{
    static void Main(string[] args)
    {
        var reporter = new MessageReporterConsole(debugToConsole: true, debugToFile: true);
        reporter.Info("Starting FlexGuard backup...");

        var options = new ProgramOptions("Test1", OperationMode.FullBackup);
        //var options = new ProgramOptions("TestLarge", OperationMode.FullBackup);
        //var options = new ProgramOptions("TestExLarge", OperationMode.FullBackup);
        reporter.Info($"Selected Job: {options.JobName}, Operation Mode: {options.Mode}");

        var jobConfig = JobLoader.Load(options.JobName);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        reporter.Info("Create backup file list...");
        var allFiles = FileCollector.CollectFiles(jobConfig,reporter);

        stopwatch.Stop();
        reporter.Info($"Found {allFiles.Count} files to back up.");
        reporter.Info($"Duration: {stopwatch.Elapsed:hh\\:mm\\:ss}");
    }
    
}