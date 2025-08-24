using FlexGuard.Core.Compression;
using FlexGuard.Core.Options;
using FlexGuard.Core.Reporting;
using FlexGuard.Core.Util;
using System.Globalization;

namespace FlexGuard.CLI.Options;

public static class ProgramOptionsParser
{
    public static ProgramOptions? Parse(string[] args, IMessageReporter reporter)
    {
        // Ingen args? Vis help
        if (args.Length == 0)
        {
            ShowHelp(reporter);
            return null;
        }

        // Tjek for help
        if (args.Any(a => a is "/?" or "/h" or "-h" or "--help"))
        {
            ShowHelp(reporter);
            return null;
        }

        // Tjek for version
        if (args.Any(a => a is "-v" or "--version"))
        {
            reporter.Info($"FlexGuard v{VersionHelper.GetAppVersion()}");
            return null;
        }

        var options = new ProgramOptionsBuilder();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg.StartsWith("--"))
            {
                string key = arg[2..].ToLowerInvariant();
                string? value = (i + 1 < args.Length && !args[i + 1].StartsWith('-')) ? args[++i] : null;

                switch (key)
                {
                    case "jobname":
                        options.JobName = value ?? throw new ArgumentException("Missing value for --jobname");
                        break;

                    case "mode":
                        if (string.IsNullOrWhiteSpace(value))
                            throw new ArgumentException("Missing value for --mode");

                        options.Mode = value.ToLowerInvariant() switch
                        {
                            "full" => OperationMode.FullBackup,
                            "diff" or "differential" => OperationMode.DifferentialBackup,
                            "restore" => OperationMode.Restore,
                            _ => throw new ArgumentException($"Invalid value for --mode: {value}. Valid values are full, diff, restore.")
                        };
                        break;

                    case "maxfiles":
                        options.MaxFilesPerGroup = ParseInt(value, "--maxfiles");
                        break;

                    case "maxbytes":
                        options.MaxBytesPerGroup = ParseLong(value, "--maxbytes");
                        break;

                    case "compression":
                        if (Enum.TryParse<CompressionMethod>(value, true, out var comp))
                            options.Compression = comp;
                        else
                            throw new ArgumentException($"Invalid value for --compression: {value}");
                        break;

                    case "measure-compression":
                        options.EnableCompressionRatioMeasurement = true;
                        break;

                    default:
                        throw new ArgumentException($"Unknown argument: {arg}");
                }
            }
            else
            {
                throw new ArgumentException($"Invalid argument format: {arg}");
            }
        }

        return options.Build();
    }

    private static int ParseInt(string? value, string argName)
    {
        if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;
        throw new ArgumentException($"Invalid integer for {argName}: {value}");
    }

    private static long ParseLong(string? value, string argName)
    {
        if (long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;
        throw new ArgumentException($"Invalid number for {argName}: {value}");
    }

    private static void ShowHelp(IMessageReporter reporter)
    {
        reporter.Info("FlexGuard Backup Tool - Options:");
        reporter.WriteRaw("  --jobname <name>                  Name of the backup job.");
        reporter.WriteRaw("  --mode <full|diff|restore>        Operation mode (Full, Differential, or Restore).");
        reporter.WriteRaw("  --maxfiles <int>                  Max files per group (default: 1000).");
        reporter.WriteRaw("  --maxbytes <long>                 Max bytes per group (default: 1GB).");
        reporter.WriteRaw("  --compression <gzip|brotli|zstd>  Compression method (default: zstd).");
        reporter.WriteRaw("  --measure-compression             Enable compression ratio measurement.");
        reporter.WriteRaw("  -v, --version                     Show version.");
        reporter.WriteRaw("  /?, /h, -h, --help                Show this help.");
    }
}