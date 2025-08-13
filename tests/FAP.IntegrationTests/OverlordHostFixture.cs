using System;
using System.Net.Http;
using System.Threading.Tasks;
using FAP.Domain.Entities;
using FAP.Domain.Handlers;
using FAP.Domain.Services;
using FAP.Network.Server;
using FAP.Network.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace FAP.IntegrationTests
{
    // Spins up an overlord (server) on 127.0.0.1:40 for tests, using in-process composition
    public sealed class OverlordHostFixture : IAsyncLifetime
    {
        public IServiceProvider Services { get; private set; } = null!;
        public ListenerService Listener { get; private set; } = null!;
        public Model Model { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            // Reduce framework/server log noise for test runs
            Environment.SetEnvironmentVariable("Logging__LogLevel__Default", "Warning");
            Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft", "Warning");
            Environment.SetEnvironmentVariable("Logging__LogLevel__System", "Warning");
            Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft.Hosting.Lifetime", "Error");
            Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft.AspNetCore", "Error");
            Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft.AspNetCore.Server.Kestrel", "Error");

            var services = new ServiceCollection();
            services.AddLogging(b =>
            {
                b.ClearProviders();
                b.AddConsole();
                // Quiet server output during tests
                b.SetMinimumLevel(LogLevel.Warning);
                b.AddFilter("Microsoft.*", LogLevel.Warning);
                b.AddFilter("System.*", LogLevel.Warning);
                b.AddFilter("FAP.*", LogLevel.Warning);
            });
            services.AddOptions();
            services.Configure<FapListenOptions>(o => { o.Address = "127.0.0.1"; o.Port = 40; });
            services.Configure<FapWebOptions>(_ => { });

            services.AddSingleton<Model>();
            services.AddSingleton<ShareInfoService>();
            services.AddSingleton<BufferService>();
            services.AddSingleton<ServerUploadLimiterService>();
            services.AddSingleton<ModernHTTPHandler>();
            services.AddSingleton<MulticastClientService>();
            services.AddSingleton<MulticastServerService>();
            services.AddSingleton<FAP.Domain.Net.LANPeerFinderService>();
            services.AddSingleton<ListenerService>(sp => new ListenerService(sp, true, sp.GetRequiredService<ILogger<ListenerService>>()));

            Services = services.BuildServiceProvider();
            Model = Services.GetRequiredService<Model>();
            Model.LocalNode.Host = "127.0.0.1";
            Model.LocalNode.Port = 40;
            Model.IsDedicated = true;
            Model.Nickname = "OverlordTest";
            Model.CheckSetDefaults();

            Listener = Services.GetRequiredService<ListenerService>();
            Listener.Start(40);

            // Wait for health
            using var http = new HttpClient();
            var resp = await http.GetAsync("http://127.0.0.1:40/Fap.api/health");
            resp.EnsureSuccessStatusCode();
        }

        public Task DisposeAsync()
        {
            try { Listener?.Stop(); } catch { }
            return Task.CompletedTask;
        }
    }
}


