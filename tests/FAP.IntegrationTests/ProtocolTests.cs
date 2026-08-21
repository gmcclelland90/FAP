using System;
using System.Threading;
using System.Net.Http;
using System.Threading.Tasks;
using FAP.Domain;
using FAP.Domain.Entities;
using FAP.Domain.Net;
using FAP.Domain.Verbs;
using FAP.Testing;
using Xunit;

namespace FAP.IntegrationTests;

 [Collection("Overlord")]
public class ProtocolTests
{
    public ProtocolTests() { }
        private static async Task<bool> IsServerHealthyAsync(string serverUrl)
        {
            try
            {
                using var http = new HttpClient();
                var baseUrl = serverUrl.EndsWith("/") ? serverUrl : serverUrl + "/";
                var healthUrl = new Uri(new Uri(baseUrl), "Fap.api/health");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var resp = await http.GetAsync(healthUrl, cts.Token);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    [Fact]
    public async Task Connect_Info_succeeds()
    {
        var serverUrl = Environment.GetEnvironmentVariable("FAP_SERVER_URL");
        if (string.IsNullOrWhiteSpace(serverUrl)) serverUrl = "http://127.0.0.1:40";

        // Overlord is hosted by fixture

        // CONNECT via ModernHttpClient to avoid path mismatch
        var sourceId = Fap.Foundation.Services.IDService.CreateID();
        var clientNode = new Node { ID = sourceId, Nickname = "TestClient", Host = "127.0.0.1", Port = 0 };
        using var http = new HttpClient();
        var httpClient = new ModernHttpClient(clientNode, Microsoft.Extensions.Logging.Abstractions.NullLogger<ModernHttpClient>.Instance, http);
        var connect = new ConnectVerb { Address = clientNode.Location, ClientType = ClientType.Client, Secret = Fap.Foundation.Services.IDService.CreateID() };
        // ModernHttpClient expects destination as host:port
        var uri = new Uri(serverUrl);
        var hostPort = uri.IsDefaultPort ? $"{uri.Host}:40" : $"{uri.Host}:{uri.Port}";
        var okConnect = await httpClient.ExecuteAsync(connect, hostPort, connect.Secret, 5000);
        Assert.True(okConnect, "CONNECT failed");
        System.Console.WriteLine($"[Overlord] CONNECT ok source={sourceId}");
        var secret = connect.Secret;

        // Prepare node in client with session headers
        var serverNode = new Node { Location = hostPort, Secret = secret };

        var client = new Client(new Node { ID = sourceId });

        // INFO
        var infoReq = new FAP.Shared.Entities.NetworkRequest { Verb = "INFO", Data = string.Empty };
        var okInfo = client.Execute(infoReq, serverNode, 5000);
        System.Console.WriteLine($"[Overlord] INFO {(okInfo ? "ok" : "fail")}");
        Assert.True(okInfo, "INFO failed");

        // Overlord does not serve BROWSE; that's client-to-client. Info suffices here.
    }

    [Fact]
    public async Task Connect_NoOp_Chat_succeed()
    {
        var serverUrl = Environment.GetEnvironmentVariable("FAP_SERVER_URL");
        if (string.IsNullOrWhiteSpace(serverUrl)) serverUrl = "http://127.0.0.1:40";

        // Overlord is hosted by fixture

        var sourceId = Fap.Foundation.Services.IDService.CreateID();
        var clientNode = new Node { ID = sourceId, Nickname = "TestClient", Host = "127.0.0.1", Port = 0 };
        using var http = new HttpClient();
        var httpClient = new ModernHttpClient(clientNode, Microsoft.Extensions.Logging.Abstractions.NullLogger<ModernHttpClient>.Instance, http);
        var connect = new ConnectVerb { Address = clientNode.Location, ClientType = ClientType.Client, Secret = Fap.Foundation.Services.IDService.CreateID() };
        var uri = new Uri(serverUrl);
        var hostPort = uri.IsDefaultPort ? $"{uri.Host}:40" : $"{uri.Host}:{uri.Port}";
        var okConnect = await httpClient.ExecuteAsync(connect, hostPort, connect.Secret, 5000);
        Assert.True(okConnect, "CONNECT failed");
        var secret = connect.Secret;

        string target = hostPort;
        var serverNode = new Node { Location = target, Secret = secret };

        var client = new Client(new Node { ID = sourceId });

        // NOOP: use raw NetworkRequest to avoid response body deserialization
        var noopReq = new FAP.Shared.Entities.NetworkRequest { Verb = "NOOP", Data = string.Empty, SourceID = sourceId, AuthKey = secret };
        var okNoop = await httpClient.ExecuteAsync(noopReq, serverNode, 5000);
        System.Console.WriteLine($"[Overlord] NOOP {(okNoop ? "ok" : "fail")}\n");
        Assert.True(okNoop, "NOOP failed");

        // CHAT (fire and expect 200) - send raw request to avoid response body dependency
        var chatVerb = new ChatVerb { Nickname = "Test", Message = "ping", SourceID = sourceId };
        var chatReq = new FAP.Shared.Entities.NetworkRequest
        {
            Verb = "CHAT",
            Data = System.Text.Json.JsonSerializer.Serialize(chatVerb),
            SourceID = sourceId,
            AuthKey = secret
        };
        var okChat = await httpClient.ExecuteAsync(chatReq, serverNode, 5000);
        System.Console.WriteLine($"[Overlord] CHAT {(okChat ? "ok" : "fail")}");
        Assert.True(okChat, "CHAT failed");
    }
}

