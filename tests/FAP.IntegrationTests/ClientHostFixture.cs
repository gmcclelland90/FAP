using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using FAP.Domain.Entities;
using FAP.Domain.Services;
using FAP.Domain.Handlers;
using FAP.Network.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FAP.IntegrationTests
{
    // xUnit fixture that starts a client listener bound to 127.0.0.1 on a free port,
    // with a temporary share containing a known file. Exposes the model and baseUrl.
    public sealed class ClientHostFixture : IAsyncLifetime
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
            Environment.SetEnvironmentVariable("Logging__LogLevel__System", "Warning");
            Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft.Hosting.Lifetime", "Error");
            Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft.AspNetCore", "Error");
            Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft.AspNetCore.Server.Kestrel", "Error");
            // Create temp share root and seed a file
            tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fap-it-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(System.IO.Path.Combine(tempRoot, "hello.txt"), "hello world");

            // Compose minimal DI for a client listener
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
            services.AddSingleton<Model>();
            services.AddSingleton<ShareInfoService>();
            services.AddSingleton<ModernHTTPHandler>();
            services.AddSingleton<BufferService>();
            services.AddSingleton<ServerUploadLimiterService>();
            services.AddSingleton<ListenerService>(sp => new ListenerService(sp, false, sp.GetRequiredService<ILogger<ListenerService>>()));
            services.AddSingleton<FAP.Domain.Verbs.IConversationController, TestConversationController>();
            services.AddSingleton<MulticastClientService>();
            services.AddSingleton<MulticastServerService>();
            services.AddSingleton<FAP.Domain.Net.LANPeerFinderService>();
            Services = services.BuildServiceProvider();

            Model = Services.GetRequiredService<Model>();
            // Configure local node on loopback
            Model.LocalNode.Host = "127.0.0.1";
            Model.LocalNode.Port = 30;
            Model.LocalNode.Nickname = "TestClient";
            Model.CheckSetDefaults();

            // Point download folders to temp as well
            Model.DownloadFolder = System.IO.Path.Combine(tempRoot, "downloads");
            Model.IncompleteFolder = System.IO.Path.Combine(tempRoot, "incomplete");

            // Add a share to the model and refresh index so SEARCH works
            var share = new FAP.Domain.Entities.Share
            {
                Name = "Tmp",
                Path = tempRoot
            };
            Model.Shares.Add(share);
            Services.GetRequiredService<ShareInfoService>().RefreshPath(share);

            Listener = Services.GetRequiredService<ListenerService>();
            // Start on a base port unlikely to conflict with running server (pick 8030)
            Listener.Start(8030);

            // Quick health check of the client listener
            using var http = new HttpClient();
            var health = new Uri(new Uri(BaseUrl.EndsWith("/") ? BaseUrl : BaseUrl + "/"), "Fap.api/health");
            var resp = await http.GetAsync(health);
            resp.EnsureSuccessStatusCode();
        }

        public Task DisposeAsync()
        {
            try
            {
                Listener?.Stop();
            }
            catch { }
            try
            {
                if (!string.IsNullOrEmpty(tempRoot) && Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
            catch { }
            return Task.CompletedTask;
        }
    }
}


