using System;
using System.Net.Http;
using System.Threading.Tasks;
using FAP.Domain.Entities;
using FAP.Domain.Services;
using FAP.Network.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace FAP.IntegrationTests
{
    /// <summary>
    /// Spins up an overlord on 127.0.0.1:40 using shipping AddFapCore composition.
    /// </summary>
    public sealed class OverlordHostFixture : IAsyncLifetime
    {
        public IServiceProvider Services { get; private set; } = null!;
        public ListenerService Listener { get; private set; } = null!;
        public Model Model { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            var services = TestHostComposition.CreateBaseServices();
            services.Configure<FapListenOptions>(o =>
            {
                o.Address = "127.0.0.1";
                o.Port = 40;
            });
            services.Configure<FapWebOptions>(_ => { });

            Services = services.BuildServiceProvider();
            Model = Services.GetRequiredService<Model>();
            Model.LocalNode.Host = "127.0.0.1";
            Model.LocalNode.Port = 40;
            Model.IsDedicated = true;
            Model.Nickname = "OverlordTest";
            Model.CheckSetDefaults();

            Listener = TestHostComposition.CreateListener(Services, isServer: true);
            try
            {
                Listener.Start(40);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "OverlordHostFixture failed to start on 127.0.0.1:40. Stop any running FAP client/overlord using port 40, then retry.",
                    ex);
            }

            using var http = new HttpClient();
            var resp = await http.GetAsync("http://127.0.0.1:40/Fap.api/health");
            resp.EnsureSuccessStatusCode();
        }

        public Task DisposeAsync()
        {
            try { Listener?.Stop(); } catch { /* ignore */ }
            if (Services is IDisposable d)
            {
                try { d.Dispose(); } catch { /* ignore */ }
            }
            return Task.CompletedTask;
        }
    }
}
