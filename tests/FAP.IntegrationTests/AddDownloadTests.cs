using System;
using System.Net.Http;
using System.Threading.Tasks;
using FAP.Domain.Entities;
using FAP.Domain.Net;
using FAP.Shared.Entities;
using Xunit;
using Xunit.Abstractions;

namespace FAP.IntegrationTests;

public class AddDownloadTests : IClassFixture<ClientHostFixture>
{
    private readonly ClientHostFixture _fx;
    private readonly ITestOutputHelper _output;

    public AddDownloadTests(ClientHostFixture fx, ITestOutputHelper output)
    {
        _fx = fx;
        _output = output;
    }

    [Fact]
    public async Task AddDownload_rejects_bad_auth()
    {
        var uri = new Uri(_fx.BaseUrl);
        var hostPort = $"{uri.Host}:{uri.Port}";
        var clientNode = new Node { ID = Fap.Foundation.Services.IDService.CreateID(), Nickname = "IT", Host = "127.0.0.1", Port = 0 };
        using var http = new HttpClient();
        var client = new ModernHttpClient(clientNode, Microsoft.Extensions.Logging.Abstractions.NullLogger<ModernHttpClient>.Instance, http);

        var dest = new Node { Location = hostPort, Secret = "wrong-secret" };
        var req = new NetworkRequest
        {
            Verb = "ADDDOWNLOAD",
            Param = "some-node/Tmp/hello.txt",
            AuthKey = "wrong-secret"
        };
        var ok = await client.ExecuteAsync(req, dest, 5000);
        System.Console.WriteLine($"[Client] ADDDOWNLOAD bad auth ok={ok}");
        Assert.False(ok);
    }

    [Fact]
    public async Task AddDownload_accepts_matching_secret()
    {
        var uri = new Uri(_fx.BaseUrl);
        var hostPort = $"{uri.Host}:{uri.Port}";
        var clientNode = new Node { ID = Fap.Foundation.Services.IDService.CreateID(), Nickname = "IT", Host = "127.0.0.1", Port = 0 };
        using var http = new HttpClient();
        var client = new ModernHttpClient(clientNode, Microsoft.Extensions.Logging.Abstractions.NullLogger<ModernHttpClient>.Instance, http);

        var secret = _fx.Model.LocalNode.Secret;
        var dest = new Node { Location = hostPort, Secret = secret };
        var req = new NetworkRequest
        {
            Verb = "ADDDOWNLOAD",
            Param = "missing-node/Tmp/hello.txt",
            AuthKey = secret
        };
        var ok = await client.ExecuteAsync(req, dest, 5000);
        _output.WriteLine($"ADDDOWNLOAD good auth ok={ok}");
        System.Console.WriteLine($"[Client] ADDDOWNLOAD good auth ok={ok}");
        Assert.True(ok, "ADDDOWNLOAD with LocalNode.Secret should return OK even if remote node is missing");
    }
}