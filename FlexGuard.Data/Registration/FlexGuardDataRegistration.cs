using FlexGuard.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using FlexGuard.Data.Repositories.Sqlite;
using FlexGuard.Data.Configuration;
using FlexGuard.Data.Repositories.Json;

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
                    if (string.IsNullOrWhiteSpace(opts.JsonPath))
                        throw new InvalidOperationException(
                            "FlexGuardDataOptions.JsonPath must be set when Backend=Json.");
                    EnsureDir(opts.JsonPath!);
                    services.AddSingleton<IFlexTestTableStore>(_ =>
                        new JsonFlexTestTableStore(opts.JsonPath!));
                    break;

                case Backend.Sqlite:
                    if (string.IsNullOrWhiteSpace(opts.SqlitePath))
                        throw new InvalidOperationException(
                            "FlexGuardDataOptions.SqlitePath must be set when Backend=Sqlite.");
                    EnsureDir(opts.SqlitePath!);
                    services.AddSingleton<IFlexTestTableStore>(_ =>
                        new SqliteFlexTestTableStore(opts.SqlitePath!));
                    break;

                default:
                    throw new NotSupportedException($"Backend '{opts.Backend}' is not supported.");
            }

            return services;
        }

        private static void EnsureDir(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}