using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FAP.Application;
using FAP.Application.Controllers;
using FAP.Application.DependencyInjection;
using FAP.Application.Services;
using FAP.Application.ViewModels;
using FAP.Application.Views;
using FAP.Domain;
using FAP.Domain.Entities;
using FAP.Domain.Services;
using FAP.Domain.Verbs;
using FAP.Network.Server;
using FAP.Shared.ConnectTiming;
using FAP.Shared.Services;
using Fap.Foundation.Hosting;
using Fap.Foundation.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FAP.IntegrationTests;

/// <summary>
/// Full AddFapCore + AddFapClient host for time-to-connected scenarios (no UI).
/// </summary>
internal sealed class TimeToConnectedHost : IAsyncDisposable
{
    public IServiceProvider Services { get; }
    public Model Model { get; }
    public IConnectTimingProbe Probe { get; }
    public OverlordManagerService Overlord { get; }
    public bool ResolveCore { get; }

    private ApplicationCore? _core;
    private readonly string _tempRoot;

    public ApplicationCore Core =>
        _core ??= Services.GetRequiredService<ApplicationCore>();

    private TimeToConnectedHost(IServiceProvider services, string tempRoot, bool resolveCore)
    {
        Services = services;
        _tempRoot = tempRoot;
        ResolveCore = resolveCore;
        Model = services.GetRequiredService<Model>();
        Probe = services.GetRequiredService<IConnectTimingProbe>();
        Overlord = services.GetRequiredService<OverlordManagerService>();
        if (resolveCore)
            _core = services.GetRequiredService<ApplicationCore>();
    }

    public static TimeToConnectedHost CreateClient(
        int clientPort = 8031,
        int? discoveryGraceMs = null,
        string? nickname = null)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "fap-ttc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        // Isolate config/queue from the user's real LocalAppData\FAP
        Environment.SetEnvironmentVariable("FAP_DATA_FOLDER", tempRoot);

        var services = BuildServices(listenPort: clientPort, discoveryGraceMs);
        var sp = services.BuildServiceProvider();
        var host = new TimeToConnectedHost(sp, tempRoot, resolveCore: true);
        host.ConfigureModel(clientPort, nickname);
        return host;
    }

    public static TimeToConnectedHost CreateDedicatedOverlord(int clientPort = 8032)
    {
        var host = CreateClient(clientPort, discoveryGraceMs: 0);
        host.Model.IsDedicated = true;
        return host;
    }

    /// <summary>Standalone overlord on :40 for join_existing (no ApplicationCore / connect loop).</summary>
    public static async Task<TimeToConnectedHost> CreateOverlordOnlyAsync()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "fap-ttc-ol-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        Environment.SetEnvironmentVariable("FAP_DATA_FOLDER", tempRoot);

        var services = BuildServices(listenPort: 40, discoveryGraceMs: 0);
        var sp = services.BuildServiceProvider();
        var host = new TimeToConnectedHost(sp, tempRoot, resolveCore: false);
        host.Model.LocalNode.Host = "127.0.0.1";
        host.Model.LocalNode.Port = 40;
        host.Model.IsDedicated = true;
        host.Model.Nickname = "TtcOverlord";
        host.Model.DisplayedHelp = true;
        host.Model.CheckSetDefaults();
        host.Overlord.Start();

        using var http = new System.Net.Http.HttpClient();
        for (int i = 0; i < 40; i++)
        {
            try
            {
                var resp = await http.GetAsync("http://127.0.0.1:40/Fap.api/health");
                if (resp.IsSuccessStatusCode)
                    return host;
            }
            catch
            {
                // retry
            }
            await Task.Delay(100);
        }
        throw new InvalidOperationException("Overlord health check failed on 127.0.0.1:40");
    }

    public bool LoadClient()
    {
        Model.DisplayedHelp = true;
        Model.LocalNode.Host = "127.0.0.1";
        return Core.Load(false);
    }

    public void StartClient() => Core.StartClient();

    public async Task StartDedicatedAsync()
    {
        Model.DisplayedHelp = true;
        Model.LocalNode.Host = "127.0.0.1";
        Model.IsDedicated = true;
        // Load as server=true skips watchdog; dedicated path starts overlord then client.
        if (!Core.Load(true))
            throw new InvalidOperationException("ApplicationCore.Load(true) failed");
        await Core.StartOverlordServerAsync();
    }

    public async Task WaitForConnectedAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Model.Network.State == ConnectionState.Connected)
                return;
            await Task.Delay(50);
        }
        throw new TimeoutException(
            $"Timed out waiting for Connected after {timeout.TotalSeconds:0}s (state={Model.Network.State})");
    }

    public async ValueTask DisposeAsync()
    {
        // Do NOT call ApplicationCore.ShutDownAsync — it model.Save()s into the real
        // %LocalAppData%\FAP\ClientConfig.cfg and would overwrite the user's nickname.
        try { Services.GetService<WatchdogController>()?.Stop(); } catch { /* ignore */ }
        try { Services.GetService<ConnectionController>()?.Exit(); } catch { /* ignore */ }
        try { Overlord.Stop(); } catch { /* ignore */ }
        if (Services is IDisposable d)
        {
            try { d.Dispose(); } catch { /* ignore */ }
        }
        // Allow Kestrel to release :40 before the next test
        for (int i = 0; i < 30; i++)
        {
            if (!await IsPort40HealthyAsync())
                break;
            await Task.Delay(100);
        }
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, true);
        }
        catch { /* ignore */ }
    }

    private static async Task<bool> IsPort40HealthyAsync()
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(300) };
            var resp = await http.GetAsync("http://127.0.0.1:40/Fap.api/health");
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private void ConfigureModel(int clientPort, string? nickname = null)
    {
        Model.LocalNode.Host = "127.0.0.1";
        Model.LocalNode.Port = clientPort;
        Model.Nickname = string.IsNullOrWhiteSpace(nickname) ? "TtcClient" : nickname;
        Model.DisplayedHelp = true;
        Model.IsDedicated = false;
        Model.DownloadFolder = Path.Combine(_tempRoot, "downloads");
        Model.IncompleteFolder = Path.Combine(_tempRoot, "incomplete");
        Directory.CreateDirectory(Model.DownloadFolder);
        Directory.CreateDirectory(Model.IncompleteFolder);
        Model.CheckSetDefaults();
    }

    private static ServiceCollection BuildServices(int listenPort, int? discoveryGraceMs)
    {
        TestHostComposition.QuietLogging();
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
        services.Configure<FapListenOptions>(o =>
        {
            o.Address = "127.0.0.1";
            o.Port = listenPort;
        });
        int graceMs = ResolveDiscoveryGraceMs(discoveryGraceMs);
        services.Configure<FapElectionOptions>(o =>
        {
            o.DiscoveryGraceMs = graceMs;
        });
        services.AddFapCore();
        services.AddFapClient();

        var stub = new StubView();
        services.AddSingleton<IAppLifetime, NoOpAppLifetime>();
        services.AddSingleton<IUiDispatcher>(_ =>
            new SynchronizationContextUiDispatcher(SynchronizationContext.Current ?? new SynchronizationContext()));
        services.AddSingleton<IMainWindow>(_ => stub);
        services.AddSingleton<IDownloadQueue>(_ => stub);
        services.AddSingleton<ISharesView>(_ => stub);
        services.AddSingleton<ISearchView>(_ => stub);
        services.AddSingleton<ISettingsView>(_ => stub);
        services.AddSingleton<ICompareView>(_ => stub);
        services.AddSingleton<IBrowserView>(_ => stub);
        services.AddTransient<IPopupWindow>(_ => stub);
        services.AddSingleton<IShellNavigation, StubShellNavigation>();
        services.AddSingleton<IGettingStartedUi, StubGettingStarted>();
        services.AddSingleton<IConverstationView>(_ => stub);
        services.AddSingleton<IQuery, StubQuery>();
        services.AddSingleton<IMessageService, StubMessageService>();
        services.AddSingleton<IInterfaceSelectionView>(_ => stub);
        services.AddSingleton<IConversationController, TestConversationController>();
        return services;
    }

    /// <summary>
    /// Explicit arg wins; else FAP_TTC_DISCOVERY_GRACE_MS; else production default (0).
    /// </summary>
    internal static int ResolveDiscoveryGraceMs(int? explicitMs)
    {
        if (explicitMs.HasValue)
            return Math.Max(0, explicitMs.Value);
        if (int.TryParse(Environment.GetEnvironmentVariable("FAP_TTC_DISCOVERY_GRACE_MS"), out var fromEnv) && fromEnv >= 0)
            return fromEnv;
        return 0;
    }

    private sealed class StubView : IMainWindow, IDownloadQueue, ISharesView, ISearchView, ISettingsView, ICompareView,
        IBrowserView, IPopupWindow, IConverstationView, IInterfaceSelectionView
    {
        public object DataContext { get; set; } = null!;
        public bool? ShowDialog() => true;
        public void Show() { }
        public void Close() { }
        public void Flash() { }
        public void FlashIfNotActive() { }
    }

    private sealed class StubQuery : IQuery
    {
        public bool SelectFolder(out string result) { result = string.Empty; return false; }
        public bool SelectFile(out string result) { result = string.Empty; return false; }
        public bool SelectImageFile(out string result) { result = string.Empty; return false; }
    }

    private sealed class StubMessageService : IMessageService
    {
        public void ShowMessage(string message) { }
        public void ShowWarning(string message) { }
        public void ShowError(string message) { }
    }

    private sealed class StubShellNavigation : IShellNavigation
    {
        public void GoBack() { }
        public void NavigateTag(string tag) { }
        public void NavigateToChat(string? peerId = null) { }
    }

    private sealed class StubGettingStarted : IGettingStartedUi
    {
        public void ShowGettingStarted() { }
    }
}
