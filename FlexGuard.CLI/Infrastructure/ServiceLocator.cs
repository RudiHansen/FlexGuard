using Microsoft.Extensions.DependencyInjection;

namespace FlexGuard.CLI.Infrastructure;
public static class Services
{
    private static IServiceProvider? _provider;

    public static void Init(IServiceProvider provider)
        => _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    public static T Get<T>() where T : class
        => _provider!.GetRequiredService<T>();
}