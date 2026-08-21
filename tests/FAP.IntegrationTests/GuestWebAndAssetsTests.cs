using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FAP.IntegrationTests;

[Collection("Overlord")]
public class GuestWebAndAssetsTests
{
    private readonly ITestOutputHelper _output;

    public GuestWebAndAssetsTests(ITestOutputHelper output) => _output = output;

    private static string ServerUrl =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FAP_SERVER_URL"))
            ? "http://127.0.0.1:40"
            : Environment.GetEnvironmentVariable("FAP_SERVER_URL")!;

    [Fact]
    public async Task Guest_ui_razor_host_ok()
    {
        using var http = new HttpClient();
        var url = new Uri(new Uri(ServerUrl.TrimEnd('/') + "/"), "Fap.api/guest-ui");
        var resp = await http.GetAsync(url);
        _output.WriteLine($"guest-ui status={(int)resp.StatusCode}");
        System.Console.WriteLine($"[Overlord] guest-ui status={(int)resp.StatusCode}");
        Assert.True(resp.IsSuccessStatusCode, $"guest-ui status={(int)resp.StatusCode}");
        var html = await resp.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(html));
    }

    [Fact]
    public async Task Guest_static_assets_served()
    {
        using var http = new HttpClient();
        var baseUri = new Uri(ServerUrl.TrimEnd('/') + "/");
        foreach (var path in new[]
                 {
                     "Fap.app.web/css/fap.css",
                     "Fap.app.web/js/browse.js",
                     "Fap.app.web/images/fap.png",
                     "Fap.app.web/favicon.ico"
                 })
        {
            var resp = await http.GetAsync(new Uri(baseUri, path));
            System.Console.WriteLine($"[Overlord] asset {path} status={(int)resp.StatusCode}");
            Assert.True(resp.IsSuccessStatusCode, $"{path} status={(int)resp.StatusCode}");
        }
    }

    [Fact]
    public async Task Guest_browse_html_has_structure()
    {
        using var http = new HttpClient();
        var resp = await http.GetAsync(ServerUrl.TrimEnd('/') + "/");
        resp.EnsureSuccessStatusCode();
        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("id=\"files\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-label=\"Path\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("browse.js", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fap.css", html, StringComparison.OrdinalIgnoreCase);
        System.Console.WriteLine("[Overlord] guest browse structure ok");
    }
}