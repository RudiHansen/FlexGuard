using FlexGuard.CLI.Infrastructure;
using FlexGuard.Core.Compression;
using FlexGuard.Core.Config;
using FlexGuard.Core.Manifest;
using FlexGuard.Core.Models;
using FlexGuard.Core.Recording;
using Spectre.Console;
using System.Text.Json;

namespace FlexGuard.CLI.Restore;

public class RestoreFileSelector
{
    private readonly BackupJobConfig _jobConfig;
    
    public RestoreFileSelector(BackupJobConfig jobConfig)
    {
        _jobConfig = jobConfig;
    }

    public List<FlexBackupFileEntry>? SelectFiles()
    {
        var recorder = Services.Get<BackupRunRecorder>();
        var jobs = recorder.RestoreGetFlexBackupEntryForJobName(_jobConfig.JobName).GetAwaiter().GetResult();   // Get list of jobs for the given job name


        AnsiConsole.Clear();

        if (jobs is null || jobs.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No backup runs found for this job.[/]");
            return null;
        }

        // Show selection prompt to choose a backup run
        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<FlexBackupEntry>()
                .Title("Select a [green]backup run[/] to restore from")
                .PageSize(10)
                .UseConverter(e => $"{e.StartDateTimeUtc:yyyy-MM-dd HH:mm} - {e.OperationMode} - {e.JobName}")
                .AddChoices(jobs)   // nu er det en liste og ikke en Task
        );

        // TODO: Validate selected job status, and perhaps also find som way to ckeck if the backup files are accessible
        var allFiles = recorder.RestoreGetFlexBackupFileEntryForBackupEntryId(selected.BackupEntryId).GetAwaiter().GetResult(); // Get all files for the selected backup run
        if(allFiles == null)
        {
            AnsiConsole.MarkupLine("[red]No files found for the selected backup run.[/]");
            return null;
        }
        
        var selectedFiles = DirectoryViewSelector.Show(allFiles);
        return selectedFiles;
    }
}