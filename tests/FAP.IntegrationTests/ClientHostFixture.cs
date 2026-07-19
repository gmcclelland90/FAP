using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using FAP.Domain.Entities;
using FAP.Domain.Services;
using FAP.Domain.Verbs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FAP.IntegrationTests
{
    /// <summary>
    /// Client listener on 127.0.0.1:8030 with a temp share, using shipping AddFapCore composition.
    /// </summary>
    public sealed class ClientHostFixture : IAsyncLifetime
    {
        public IServiceProvider Services { get; private set; } = null!;
        public Model Model { get; private set; } = null!;
        public ListenerService Listener { get; private set; } = null!;
        public string BaseUrl => $"http://127.0.0.1:{Model.ClientPort}";
        public string TempRoot => tempRoot;
        private string tempRoot = string.Empty;

        public async Task InitializeAsync()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "fap-it-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "hello.txt"), "hello world");

            var services = TestHostComposition.CreateBaseServices();
            // Listener constructs FAPClientHandler which requires IConversationController.
            services.AddSingleton<IConversationController, TestConversationController>();

            Services = services.BuildServiceProvider();

            Model = Services.GetRequiredService<Model>();
            Model.LocalNode.Host = "127.0.0.1";
            Model.LocalNode.Port = 30;
            Model.LocalNode.Nickname = "TestClient";
            Model.LocalNode.Secret = "test-secret-" + Guid.NewGuid().ToString("N");
            Model.CheckSetDefaults();

            Model.DownloadFolder = Path.Combine(tempRoot, "downloads");
            Model.IncompleteFolder = Path.Combine(tempRoot, "incomplete");

            var share = new Share
            {
                Name = "Tmp",
                Path = tempRoot
            };
            Model.Shares.Add(share);
            Services.GetRequiredService<ShareInfoService>().RefreshPath(share);

            Listener = TestHostComposition.CreateListener(Services, isServer: false);
            Listener.Start(8030);

            using var http = new HttpClient();
            var health = new Uri(new Uri(BaseUrl.EndsWith("/") ? BaseUrl : BaseUrl + "/"), "Fap.api/health");
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
    }
}
