using System;
using System.IO;
using FAP.Application.DependencyInjection;
using FAP.Domain.Services;
using FAP.Domain.Verbs;
using FAP.Network.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FAP.IntegrationTests;

/// <summary>
/// Builds the shipping DI graph (AddFapCore) for in-process test hosts.
/// </summary>
internal static class TestHostComposition
{
    public static void QuietLogging()
    {
        Environment.SetEnvironmentVariable("Logging__LogLevel__Default", "Warning");
        Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft", "Warning");
        Environment.SetEnvironmentVariable("Logging__LogLevel__System", "Warning");
        Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft.Hosting.Lifetime", "Error");
        Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft.AspNetCore", "Error");
        Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft.AspNetCore.Server.Kestrel", "Error");
        EnsureIsolatedDataFolder();
    }

    /// <summary>
    /// Redirect Model/DownloadQueue Save/Load away from the user's real %LocalAppData%\FAP.
    /// </summary>
    public static void EnsureIsolatedDataFolder()
    {
        var current = Environment.GetEnvironmentVariable("FAP_DATA_FOLDER");
        if (!string.IsNullOrWhiteSpace(current) &&
            current.IndexOf("fap-test-", StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        var dir = Path.Combine(Path.GetTempPath(), "fap-test-data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("FAP_DATA_FOLDER", dir);
    }

    public static ServiceCollection CreateBaseServices()
    {
        QuietLogging();
        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddConsole();
            b.SetMinimumLevel(LogLevel.Warning);
            b.AddFilter("Microsoft.*", LogLevel.Warning);
            b.AddFilter("System.*", LogLevel.Warning);
            b.AddFilter("FAP.*", LogLevel.Warning);
        });
        services.AddOptions();
        services.AddFapCore();
        return services;
    }

    public static ListenerService CreateListener(IServiceProvider sp, bool isServer) =>
        sp.GetRequiredService<IListenerServiceFactory>().Create(isServer);
}