using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FAP.Domain;
using FAP.Domain.Entities;
using FAP.Domain.Net;
using FAP.Domain.Verbs;
using Xunit;
using Xunit.Abstractions;

namespace FAP.IntegrationTests
{
    public class ProtocolShareSearchDownloadTests : IClassFixture<ClientHostFixture>
    {
        private readonly ClientHostFixture _fx;
        private readonly ITestOutputHelper _output;

        public ProtocolShareSearchDownloadTests(ClientHostFixture fx, ITestOutputHelper output)
        {
            _fx = fx;
            _output = output;
        }

        [Fact]
        public async Task Connect_Search_Browse_Succeeds()
        {
            var baseUrl = _fx.BaseUrl;
            _output.WriteLine($"[Client] baseUrl={baseUrl}");
            System.Console.WriteLine($"[Client] baseUrl={baseUrl}");

            // Use host:port for ModernHttpClient
            var uri = new Uri(baseUrl);
            var hostPort = uri.IsDefaultPort ? $"{uri.Host}:40" : $"{uri.Host}:{uri.Port}";

            var clientNode = new Node { ID = Fap.Foundation.Services.IDService.CreateID(), Nickname = "IT", Host = "127.0.0.1", Port = 0 };
            using var http = new HttpClient();
            var client = new ModernHttpClient(clientNode, Microsoft.Extensions.Logging.Abstractions.NullLogger<ModernHttpClient>.Instance, http);

            // SEARCH for hello.txt (client listener accepts SEARCH without CONNECT)
            var search = new SearchVerb();
            search.SearchString = "hello.txt";
            var searchOk = await client.ExecuteAsync(search, hostPort, 5000);
            _output.WriteLine($"[Client] SEARCH {(searchOk ? "ok" : "fail")}");
            System.Console.WriteLine($"[Client] SEARCH {(searchOk ? "ok" : "fail")}");
            Assert.True(searchOk);
            Assert.NotNull(search.Results);
            Assert.Contains(search.Results, r => r.FileName.Equals("hello.txt", StringComparison.OrdinalIgnoreCase));

            // BROWSE root
            var browse = new BrowseVerb();
            browse.Path = "/Tmp";
            browse.NoCache = true;
            var browseOk = await client.ExecuteAsync(browse, hostPort, 5000);
            _output.WriteLine($"[Client] BROWSE {(browseOk ? "ok" : "fail")}");
            System.Console.WriteLine($"[Client] BROWSE {(browseOk ? "ok" : "fail")}");
            Assert.True(browseOk);
            Assert.Contains(browse.Results, r => r.Name.Equals("hello.txt", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Direct_Download_Works_With_Range()
        {
            var baseUrl = _fx.BaseUrl;
            using var http = new HttpClient();

            // Use dynamic page handler path: /Tmp/hello.txt (returns 200 or 206 when range is honored)
            var url = new Uri(new Uri(baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/"), "Tmp/hello.txt");

            // Full download
            var resp = await http.GetAsync(url);
            System.Console.WriteLine($"[Client] GET status={(int)resp.StatusCode}");
            resp.EnsureSuccessStatusCode();
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            Assert.Equal(Encoding.UTF8.GetBytes("hello world"), bytes);

            // Range download (last 5 bytes)
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Range", "bytes=-5");
            var rangeResp = await http.SendAsync(req);
            System.Console.WriteLine($"[Client] Range GET status={(int)rangeResp.StatusCode}");
            Assert.Contains(rangeResp.StatusCode, new[] { System.Net.HttpStatusCode.PartialContent, System.Net.HttpStatusCode.OK });
            var rangeBytes = await rangeResp.Content.ReadAsByteArrayAsync();
            Assert.Equal(Encoding.UTF8.GetBytes("world"), rangeBytes);
        }
    }
}


