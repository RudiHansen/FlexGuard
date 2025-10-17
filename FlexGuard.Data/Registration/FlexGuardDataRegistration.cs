using FlexGuard.Core.Abstractions;
using FlexGuard.Data.Configuration;
using FlexGuard.Data.Infrastructure;
using FlexGuard.Data.Repositories.Json;
using FlexGuard.Data.Repositories.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace FlexGuard.Data.Registration
{
    public static class FlexGuardDataRegistration
    {
        public static IServiceCollection AddFlexGuardData(
            this IServiceCollection services,
            Action<FlexGuardDataOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var opts = new FlexGuardDataOptions();
            configure(opts);

            switch (opts.Backend)
            {
                case Backend.Json:
                    {
                        // FlexTestTable (eksisterende)
                        if (string.IsNullOrWhiteSpace(opts.JsonPath))
                            throw new InvalidOperationException("FlexGuardDataOptions.JsonPath must be set when Backend=Json.");
                        EnsureDir(opts.JsonPath!);
                        services.AddSingleton<IFlexTestTableStore>(_ =>
                            new JsonFlexTestTableStore(opts.JsonPath!));

                        // NewFileManifest
                        if (string.IsNullOrWhiteSpace(opts.JsonManifestPath))
                            throw new InvalidOperationException("FlexGuardDataOptions.JsonManifestPath must be set when Backend=Json.");
                        EnsureDir(opts.JsonManifestPath!);
                        services.AddSingleton<INewFileManifestStore>(_ =>
                            new JsonNewFileManifestStore(opts.JsonManifestPath!));

                        // NewFileManifestEntry
                        if (string.IsNullOrWhiteSpace(opts.JsonManifestEntryPath))
                            throw new InvalidOperationException("FlexGuardDataOptions.JsonManifestEntryPath must be set when Backend=Json.");
                        EnsureDir(opts.JsonManifestEntryPath!);
                        services.AddSingleton<INewFileManifestEntryStore>(_ =>
                            new JsonNewFileManifestEntryStore(opts.JsonManifestEntryPath!));

                        break;
                    }

                case Backend.Sqlite:
                    {
                        if (string.IsNullOrWhiteSpace(opts.SqlitePath))
                            throw new InvalidOperationException("FlexGuardDataOptions.SqlitePath must be set when Backend=Sqlite.");
                        EnsureDir(opts.SqlitePath!);

                        // Alle tre stores bruger samme db-fil
                        services.AddSingleton<IFlexTestTableStore>(_ =>
                            new SqliteFlexTestTableStore(opts.SqlitePath!));
                        services.AddSingleton<INewFileManifestStore>(_ =>
                            new SqliteNewFileManifestStore(opts.SqlitePath!));
                        services.AddSingleton<INewFileManifestEntryStore>(_ =>
                            new SqliteNewFileManifestEntryStore(opts.SqlitePath!));
                        break;
                    }

                default:
                    throw new NotSupportedException($"Backend '{opts.Backend}' is not supported.");
            }

            return services;
        }

        private static void EnsureDir(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }
    }
}