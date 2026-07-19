using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using FAP.Application.DependencyInjection;
using FAP.Domain.Entities;
using FAP.Domain.Services;
using FAP.Domain.Verbs;
using FAP.Network.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FAP.GuestWeb.UITests;

/// <summary>
/// Client listener with a seeded share for Playwright guest-web tests (port 8040).
/// </summary>
public sealed class GuestHostFixture : IAsyncLifetime
{
    public IServiceProvider Services { get; private set; } = null!;
    public Model Model { get; private set; } = null!;
    public ListenerService Listener { get; private set; } = null!;
    public string BaseUrl => $"http://127.0.0.1:{Model.ClientPort}";
    private string tempRoot = string.Empty;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("Logging__LogLevel__Default", "Warning");
        Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft", "Warning");
        Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft.AspNetCore", "Error");

        tempRoot = Path.Combine(Path.GetTempPath(), "fap-pw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(Path.Combine(tempRoot, "subdir"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "empty"));
        await File.WriteAllTextAsync(Path.Combine(tempRoot, "hello.txt"), "hello world");
        await File.WriteAllTextAsync(Path.Combine(tempRoot, "subdir", "nested.txt"), "nested");

        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.SetMinimumLevel(LogLevel.Warning);
        });
        services.AddOptions();
        services.AddFapCore();
        services.AddSingleton<IConversationController, NoopConversationController>();
        Services = services.BuildServiceProvider();

        Model = Services.GetRequiredService<Model>();
        Model.LocalNode.Host = "127.0.0.1";
        Model.LocalNode.Port = 30;
        Model.LocalNode.Nickname = "PlaywrightGuest";
        Model.CheckSetDefaults();
        Model.DownloadFolder = Path.Combine(tempRoot, "downloads");
        Model.IncompleteFolder = Path.Combine(tempRoot, "incomplete");

        var share = new Share { Name = "Tmp", Path = tempRoot };
        Model.Shares.Add(share);
        Services.GetRequiredService<ShareInfoService>().RefreshPath(share);

        Listener = Services.GetRequiredService<IListenerServiceFactory>().Create(false);
        Listener.Start(8040);

        using var http = new HttpClient();
        var health = new Uri(new Uri(BaseUrl.TrimEnd('/') + "/"), "Fap.api/health");
        var resp = await http.GetAsync(health);
        resp.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync()
    {
        try { Listener?.Stop(); } catch { /* ignore */ }
        if (Services is IDisposable d)
        {
            try { d.Dispose(); } catch { /* ignore */ }
        }
        try
        {
            if (!string.IsNullOrEmpty(tempRoot) && Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
        catch { /* ignore */ }
        return Task.CompletedTask;
    }

    private sealed class NoopConversationController : IConversationController
    {
        public bool HandleMessage(string id, string nickname, string message) => true;
    }
}