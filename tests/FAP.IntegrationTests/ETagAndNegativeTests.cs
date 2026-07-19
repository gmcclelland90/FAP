using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FAP.IntegrationTests;

public class ETagAndNegativeTests : IClassFixture<ClientHostFixture>
{
    private readonly ClientHostFixture _fx;
    private readonly ITestOutputHelper _output;

    public ETagAndNegativeTests(ClientHostFixture fx, ITestOutputHelper output)
    {
        _fx = fx;
        _output = output;
    }

    [Fact]
    public async Task File_Get_honors_ETag_IfNoneMatch()
    {
        using var http = new HttpClient();
        var url = new Uri(new Uri(_fx.BaseUrl.TrimEnd('/') + "/"), "Tmp/hello.txt");

        var first = await http.GetAsync(url);
        first.EnsureSuccessStatusCode();
        Assert.True(first.Headers.ETag != null || first.Headers.Contains("ETag"), "Expected ETag on first GET");
        var etag = first.Headers.ETag?.Tag ?? first.Headers.GetValues("ETag").First();
        _output.WriteLine($"ETag={etag}");

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var second = await http.SendAsync(req);
        System.Console.WriteLine($"[Client] If-None-Match status={(int)second.StatusCode}");
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task Unknown_download_path_does_not_return_seeded_file_bytes()
    {
        using var http = new HttpClient();
        var url = new Uri(new Uri(_fx.BaseUrl.TrimEnd('/') + "/"), "Tmp/does-not-exist-" + Guid.NewGuid().ToString("N") + ".txt");
        var resp = await http.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        System.Console.WriteLine($"[Client] unknown path status={(int)resp.StatusCode} len={body.Length}");
        Assert.DoesNotContain("hello world", body, StringComparison.Ordinal);
    }
}