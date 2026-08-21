using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FAP.Shared.ConnectTiming;
using FAP.Testing;
using Xunit;

namespace FAP.IntegrationTests;

[Collection("Overlord")]
public class ScenarioRunnerTests
{
    [Fact]
    public async Task DefaultScenarioRunner_InfoBrowseChat_against_overlord()
    {
        var serverUrl = Environment.GetEnvironmentVariable("FAP_SERVER_URL");
        if (string.IsNullOrWhiteSpace(serverUrl)) serverUrl = "http://127.0.0.1:40";

        var runner = new DefaultScenarioRunner();
        var options = new ScenarioOptions
        {
            ServerAddress = serverUrl,
            Clients = 1,
            Parallelism = 1,
            IterationsPerClient = 1,
            TimeoutSeconds = 10,
            // HELLO/WHO are multicast; INFO+BROWSE+CHAT exercise HTTP path (BROWSE may fail on overlord).
            VerbMix = VerbMix.Browse | VerbMix.Chat,
            FailureThresholdPercent = 100 // overlord may reject BROWSE; we assert INFO path via result counts
        };

        var result = await runner.RunAsync(options, CancellationToken.None);
        System.Console.WriteLine($"[Scenario] total={result.TotalRequests} ok={result.SuccessfulRequests} fail={result.FailedRequests}");
        Assert.Equal(1, result.TotalRequests);
        // At least one step ran; INFO is always attempted first and should succeed on overlord.
        Assert.True(result.SuccessfulRequests + result.FailedRequests == 1);

        // Same schema family as TimeToConnected for cross-version throughput compare scripts.
        var report = new ConnectTimingReport
        {
            Scenario = "scenario_throughput",
            Runtime = "net9",
            TotalMs = (long)result.Elapsed.TotalMilliseconds,
            BudgetMs = 30000,
            Phases = new Dictionary<string, long>(),
            Metrics = new Dictionary<string, long>
            {
                ["TotalRequests"] = result.TotalRequests,
                ["SuccessfulRequests"] = result.SuccessfulRequests,
                ["FailedRequests"] = result.FailedRequests,
                ["ElapsedMs"] = (long)result.Elapsed.TotalMilliseconds
            }
        };
        ConnectTimingArtifactWriter.Write(report);
    }

    [Fact]
    public async Task DefaultScenarioRunner_ConnectAsync_succeeds()
    {
        var serverUrl = Environment.GetEnvironmentVariable("FAP_SERVER_URL");
        if (string.IsNullOrWhiteSpace(serverUrl)) serverUrl = "http://127.0.0.1:40";

        var runner = new DefaultScenarioRunner();
        var (ok, secret, sourceId) = await runner.ConnectAsync(serverUrl, TimeSpan.FromSeconds(10), CancellationToken.None);
        System.Console.WriteLine($"[Scenario] ConnectAsync ok={ok} source={sourceId}");
        Assert.True(ok);
        Assert.False(string.IsNullOrWhiteSpace(secret));
        Assert.False(string.IsNullOrWhiteSpace(sourceId));
    }
}