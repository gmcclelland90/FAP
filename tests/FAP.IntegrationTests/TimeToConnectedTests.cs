using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FAP.Domain;
using FAP.Domain.Entities;
using FAP.Domain.Net;
using FAP.Domain.Verbs;
using FAP.Shared.ConnectTiming;
using Fap.Foundation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FAP.IntegrationTests;

/// <summary>
/// End-to-end open → Connected latency. Writes JSON under artifacts/connect-timing/ for agent loops.
/// Budgets default to current baseline (env FAP_TTC_BUDGET_MS_* overrides). Target after optimization:
/// solo &lt; 3000ms, join &lt; 1500ms, multi &lt; 5000ms, dedicated &lt; 2500ms.
/// Sweep discovery via FAP_TTC_DISCOVERY_GRACE_MS.
/// </summary>
[Collection("TimeToConnected")]
public class TimeToConnectedTests
{
    private static long BudgetMs(string scenario, long fallback) =>
        long.TryParse(Environment.GetEnvironmentVariable($"FAP_TTC_BUDGET_MS_{scenario.ToUpperInvariant()}"), out var v)
            ? v
            : long.TryParse(Environment.GetEnvironmentVariable("FAP_TTC_BUDGET_MS"), out var all)
                ? all
                : fallback;

    [Fact]
    public async Task SoloElect_reaches_Connected_within_budget()
    {
        await EnsurePort40FreeAsync();
        await using var host = TimeToConnectedHost.CreateClient(clientPort: 8031);
        host.Probe.Enabled = true;
        host.Probe.Reset();

        Assert.True(host.LoadClient(), "Load(false) failed");
        host.StartClient();
        await host.WaitForConnectedAsync(TimeSpan.FromSeconds(45));

        var budget = BudgetMs("solo_elect", 15000);
        var report = ConnectTimingReportBuilder.Build(host.Probe, "solo_elect", "net9", budget);
        ConnectTimingArtifactWriter.Write(report);

        Assert.True(report.TotalMs <= budget,
            $"solo_elect totalMs={report.TotalMs} exceeded budgetMs={budget}. Phases={FormatPhases(report)}");
        System.Console.WriteLine($"[TimeToConnected] solo_elect ok totalMs={report.TotalMs}");
    }

    [Fact]
    public async Task JoinExisting_reaches_Connected_within_budget()
    {
        await EnsurePort40FreeAsync();
        await using var overlord = await TimeToConnectedHost.CreateOverlordOnlyAsync();
        // Give announce a tick before client WHO
        await Task.Delay(200);

        // Use same grace as solo/multi (env or default) so join validates the swept window.
        await using var host = TimeToConnectedHost.CreateClient(clientPort: 8033);
        host.Probe.Enabled = true;
        host.Probe.Reset();

        Assert.True(host.LoadClient(), "Load(false) failed");
        host.StartClient();
        await host.WaitForConnectedAsync(TimeSpan.FromSeconds(30));

        var budget = BudgetMs("join_existing", 8000);
        var report = ConnectTimingReportBuilder.Build(host.Probe, "join_existing", "net9", budget);
        ConnectTimingArtifactWriter.Write(report);

        Assert.True(report.TotalMs <= budget,
            $"join_existing totalMs={report.TotalMs} exceeded budgetMs={budget}. Phases={FormatPhases(report)}");
        // Should not need self-elect when Hello is heard
        Assert.False(report.Phases.ContainsKey(ConnectTimingPhases.ElectStart),
            "join_existing should not self-elect; discovery may be broken (Peers vs Nodes).");
        System.Console.WriteLine($"[TimeToConnected] join_existing ok totalMs={report.TotalMs}");
    }

    [Fact]
    public async Task Dedicated_reaches_Connected_within_budget()
    {
        await EnsurePort40FreeAsync();
        await using var host = TimeToConnectedHost.CreateDedicatedOverlord(clientPort: 8034);
        host.Probe.Enabled = true;
        host.Probe.Reset();

        await host.StartDedicatedAsync();
        await host.WaitForConnectedAsync(TimeSpan.FromSeconds(30));

        var budget = BudgetMs("dedicated", 10000);
        var report = ConnectTimingReportBuilder.Build(host.Probe, "dedicated", "net9", budget);
        ConnectTimingArtifactWriter.Write(report);

        Assert.True(report.TotalMs <= budget,
            $"dedicated totalMs={report.TotalMs} exceeded budgetMs={budget}. Phases={FormatPhases(report)}");
        System.Console.WriteLine($"[TimeToConnected] dedicated ok totalMs={report.TotalMs}");
    }

    /// <summary>
    /// N clients start together with no overlord — exactly one must elect; all must reach Connected.
    /// Repeats FAP_TTC_MULTI_REPEATS times (default 5) so races are not one-shot luck.
    /// Artifact: latest-net9-multi_cold_start.json from the last successful iteration.
    /// </summary>
    [Fact]
    public async Task MultiColdStart_one_overlord_all_Connected_within_budget()
    {
        int repeats = 5;
        if (int.TryParse(Environment.GetEnvironmentVariable("FAP_TTC_MULTI_REPEATS"), out var r) && r >= 1)
            repeats = Math.Min(r, 20);

        int clientCount = 3;
        if (int.TryParse(Environment.GetEnvironmentVariable("FAP_TTC_MULTI_CLIENT_COUNT"), out var n) && n >= 2)
            clientCount = Math.Min(n, 6);

        var budget = BudgetMs("multi_cold_start", 20000);
        ConnectTimingReport? lastReport = null;

        for (int iter = 0; iter < repeats; iter++)
        {
            await EnsurePort40FreeAsync();
            var report = await RunMultiColdStartOnceAsync(clientCount, budget);
            lastReport = report;

            Assert.Equal(1, report.Metrics["OverlordCount"]);
            Assert.True(report.Metrics["ElectAttempts"] >= 1,
                $"iter {iter}: at least one client should attempt election when starting with empty Peers.");
            Assert.True(report.TotalMs <= budget,
                $"iter {iter}: multi_cold_start totalMs={report.TotalMs} exceeded budgetMs={budget}. " +
                $"metrics={FormatMetrics(report)} clients={FormatClients(report)}");

            System.Console.WriteLine(
                $"[TimeToConnected] multi_cold_start iter={iter + 1}/{repeats} ok totalMs={report.TotalMs} " +
                $"OverlordCount={report.Metrics["OverlordCount"]} ElectAttempts={report.Metrics["ElectAttempts"]} " +
                $"SpreadMs={report.Metrics["SpreadMs"]}");
        }

        Assert.NotNull(lastReport);
        lastReport!.Metrics["Repeats"] = repeats;
        ConnectTimingArtifactWriter.Write(lastReport);
    }

    private static async Task<ConnectTimingReport> RunMultiColdStartOnceAsync(int clientCount, long budget)
    {
        var hosts = new List<TimeToConnectedHost>(clientCount);
        try
        {
            for (int i = 0; i < clientCount; i++)
            {
                var host = TimeToConnectedHost.CreateClient(
                    clientPort: 8040 + i,
                    nickname: $"TtcMulti{i}");
                host.Probe.Enabled = true;
                hosts.Add(host);
            }

            for (int i = 0; i < hosts.Count; i++)
            {
                var host = hosts[i];
                Assert.True(host.LoadClient(), $"Load(false) failed for client {i}");
                host.Model.Nickname = $"TtcMulti{i}";
                host.Model.LocalNode.Host = "127.0.0.1";
                host.Model.LocalNode.Port = 8040 + i;
            }

            foreach (var host in hosts)
                host.Probe.Reset();

            var wall = Stopwatch.StartNew();
            foreach (var host in hosts)
                host.StartClient();

            await Task.WhenAll(hosts.Select(h => h.WaitForConnectedAsync(TimeSpan.FromSeconds(60))));
            wall.Stop();

            var clientReports = new List<ConnectTimingClientReport>();
            for (int i = 0; i < hosts.Count; i++)
            {
                var host = hosts[i];
                var phases = new Dictionary<string, long>(host.Probe.Snapshot());
                long total = phases.TryGetValue(ConnectTimingPhases.Connected, out var c)
                    ? c
                    : host.Probe.ElapsedMs;
                clientReports.Add(new ConnectTimingClientReport
                {
                    Id = $"c{i}",
                    Nickname = host.Model.Nickname,
                    Port = host.Model.LocalNode.Port,
                    Elected = host.Overlord.IsOverlordActive,
                    TotalMs = total,
                    Phases = phases,
                    Metrics = ConnectTimingReportBuilder.BuildPhaseMetrics(phases)
                });
            }

            var report = ConnectTimingReportBuilder.BuildMulti(
                clientReports, "multi_cold_start", "net9", budget, wall.ElapsedMilliseconds);

            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var health = await http.GetAsync("http://127.0.0.1:40/Fap.api/health");
            Assert.True(health.IsSuccessStatusCode, "Expected single overlord health on :40 after multi_cold_start");

            return report;
        }
        finally
        {
            foreach (var host in hosts)
            {
                try { await host.DisposeAsync(); }
                catch { /* ignore */ }
            }
        }
    }

    [Fact]
    public async Task ConnectRtt_against_running_overlord_emits_metric()
    {
        await EnsurePort40FreeAsync();
        await using var overlord = await TimeToConnectedHost.CreateOverlordOnlyAsync();
        overlord.Probe.Enabled = true;
        overlord.Probe.Reset();

        // Separate client listener so reverse INFO succeeds; huge grace so this host never elects
        await using var clientHost = TimeToConnectedHost.CreateClient(clientPort: 8099, discoveryGraceMs: 600_000);
        var listen = TestHostComposition.CreateListener(clientHost.Services, isServer: false);
        listen.Start(8099);

        overlord.Probe.Mark(ConnectTimingPhases.ConnectStart);
        var sw = Stopwatch.StartNew();
        var connect = new ConnectVerb
        {
            Address = clientHost.Model.LocalNode.Location,
            ClientType = ClientType.Client,
            Secret = IDService.CreateID()
        };
        var httpLogger = overlord.Services.GetRequiredService<ILogger<ModernHttpClient>>();
        var factory = overlord.Services.GetRequiredService<System.Net.Http.IHttpClientFactory>();
        var http = new ModernHttpClient(overlord.Model.LocalNode, httpLogger, factory.CreateClient("FapDefault"));
        var ok = await http.ExecuteAsync(connect, "127.0.0.1:40", connect.Secret, 5000);
        sw.Stop();
        Assert.True(ok, "CONNECT failed");

        overlord.Probe.Mark(ConnectTimingPhases.Connected);
        var budget = BudgetMs("connect_rtt", 5000);
        var report = ConnectTimingReportBuilder.Build(overlord.Probe, "connect_rtt", "net9", budget);
        report.Metrics["ConnectRttMs"] = sw.ElapsedMilliseconds;
        ConnectTimingArtifactWriter.Write(report);

        Assert.True(sw.ElapsedMilliseconds <= budget, $"ConnectRttMs={sw.ElapsedMilliseconds} > budget={budget}");
        System.Console.WriteLine(
            $"[TimeToConnected] connect_rtt ok ConnectRttMs={sw.ElapsedMilliseconds} ReverseInfoMs={report.Metrics.GetValueOrDefault("ReverseInfoMs")}");

        try { listen.Stop(); } catch { /* ignore */ }
    }

    private static string FormatPhases(ConnectTimingReport report) =>
        string.Join(", ", report.Phases);

    private static string FormatMetrics(ConnectTimingReport report) =>
        string.Join(", ", report.Metrics.Select(kv => $"{kv.Key}={kv.Value}"));

    private static string FormatClients(ConnectTimingReport report) =>
        report.Clients == null
            ? ""
            : string.Join("; ", report.Clients.Select(c =>
                $"{c.Id}:{c.Nickname}:elected={c.Elected}:totalMs={c.TotalMs}"));

    private static async Task EnsurePort40FreeAsync()
    {
        for (int i = 0; i < 50; i++)
        {
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(300) };
                var resp = await http.GetAsync("http://127.0.0.1:40/Fap.api/health");
                if (!resp.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                return;
            }
            await Task.Delay(100);
        }
        throw new InvalidOperationException(
            "Port 40 is already in use (FAP health responded). Stop the running FAP client/overlord before TimeToConnected tests.");
    }
}
