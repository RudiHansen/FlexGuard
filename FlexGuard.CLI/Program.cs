using FlexGuard.Core.Config;
using FlexGuard.Core.Options;

class Program
{
    static void Main(string[] args)
    {
        var options = new ProgramOptions("Test1", OperationMode.FullBackup);
        var jobConfig = JobLoader.Load(options.JobName);
    }
    
}