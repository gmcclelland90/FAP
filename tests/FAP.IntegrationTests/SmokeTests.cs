using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FAP.Testing;
using Xunit;
using Xunit.Abstractions;

namespace FAP.IntegrationTests;

 [Collection("Overlord")]
public class SmokeTests
{
	private readonly ITestOutputHelper _output;
    // Ensure overlord is hosted for this class' lifetime
    public SmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

	[Fact]
	public async Task Health_and_TypedCompare_succeed()
	{
		var serverUrl = Environment.GetEnvironmentVariable("FAP_SERVER_URL");
		if (string.IsNullOrWhiteSpace(serverUrl)) serverUrl = "http://127.0.0.1:40";
		_output.WriteLine($"Using serverUrl={serverUrl}");
		System.Console.WriteLine($"[Overlord] baseUrl={serverUrl}");
		await WaitForHealthAsync(serverUrl, TimeSpan.FromSeconds(15));
		System.Console.WriteLine("[Overlord] HEALTH ok");

		using var http = new HttpClient();
		var compare = new Uri(new Uri(serverUrl.EndsWith("/") ? serverUrl : serverUrl + "/"), "Fap.api/compare/v1");
		var resp = await http.GetAsync(compare);
		Assert.True(resp.IsSuccessStatusCode, $"compare/v1 status={(int)resp.StatusCode}");
		var json = await resp.Content.ReadAsStringAsync();
		Assert.False(string.IsNullOrWhiteSpace(json));
		System.Console.WriteLine("[Overlord] COMPARE ok");
	}

	[Fact]
	public async Task Static_template_served()
	{
		var serverUrl = Environment.GetEnvironmentVariable("FAP_SERVER_URL");
		if (string.IsNullOrWhiteSpace(serverUrl)) serverUrl = "http://127.0.0.1:40";
		await WaitForHealthAsync(serverUrl, TimeSpan.FromSeconds(10));
		using var http = new HttpClient();
		var url = new Uri(new Uri(serverUrl.EndsWith("/") ? serverUrl : serverUrl + "/"), "Fap.app.web/template.html");
		var resp = await http.GetAsync(url);
		Assert.True(resp.IsSuccessStatusCode, $"template.html status={(int)resp.StatusCode}");
		var html = await resp.Content.ReadAsStringAsync();
		Assert.Contains("<html", html, StringComparison.OrdinalIgnoreCase);
	}

	private static async Task WaitForHealthAsync(string baseUrl, TimeSpan timeout)
	{
		using var http = new HttpClient();
		var end = DateTime.UtcNow + timeout;
		var health = new Uri(new Uri(baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/"), "Fap.api/health");
		Exception? last = null;
		while (DateTime.UtcNow < end)
		{
			try
			{
				var resp = await http.GetAsync(health);
				if (resp.IsSuccessStatusCode) return;
				last = new Exception($"Health returned {(int)resp.StatusCode}");
			}
			catch (Exception ex)
			{
				last = ex;
			}
			await Task.Delay(500);
		}
		throw new TimeoutException($"Server health not ready at {health}: {last?.Message}");
	}
}

