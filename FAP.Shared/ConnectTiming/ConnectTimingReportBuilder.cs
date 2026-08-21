using System.Collections.Generic;
using System.Linq;

namespace FAP.Shared.ConnectTiming
{
    public static class ConnectTimingReportBuilder
    {
        public static ConnectTimingReport Build(
            IConnectTimingProbe probe,
            string scenario,
            string runtime,
            long budgetMs)
        {
            var phases = new Dictionary<string, long>(probe.Snapshot());
            long total = phases.TryGetValue(ConnectTimingPhases.Connected, out var connected)
                ? connected
                : probe.ElapsedMs;

            return new ConnectTimingReport
            {
                Version = 1,
                Scenario = scenario,
                Runtime = runtime,
                TotalMs = total,
                BudgetMs = budgetMs,
                Phases = phases,
                Metrics = BuildPhaseMetrics(phases)
            };
        }

        /// <summary>
        /// Aggregate report for N clients started together (multi_cold_start).
        /// <paramref name="totalMs"/> is wall time until the last client Connected.
        /// </summary>
        public static ConnectTimingReport BuildMulti(
            IReadOnlyList<ConnectTimingClientReport> clients,
            string scenario,
            string runtime,
            long budgetMs,
            long wallClockTotalMs)
        {
            var electAttempts = clients.Count(c => c.Phases.ContainsKey(ConnectTimingPhases.ElectStart));
            var overlordCount = clients.Count(c => c.Elected);
            var totals = clients.Select(c => c.TotalMs).OrderBy(x => x).ToList();
            var min = totals.Count > 0 ? totals[0] : wallClockTotalMs;
            var max = totals.Count > 0 ? totals[^1] : wallClockTotalMs;

            var elected = clients.FirstOrDefault(c => c.Elected);
            var phases = elected?.Phases ?? new Dictionary<string, long>();

            var metrics = new Dictionary<string, long>
            {
                ["ClientCount"] = clients.Count,
                ["ElectAttempts"] = electAttempts,
                ["OverlordCount"] = overlordCount,
                ["MinTotalMs"] = min,
                ["MaxTotalMs"] = max,
                ["SpreadMs"] = max - min
            };

            return new ConnectTimingReport
            {
                Version = 1,
                Scenario = scenario,
                Runtime = runtime,
                TotalMs = wallClockTotalMs > 0 ? wallClockTotalMs : max,
                BudgetMs = budgetMs,
                Phases = phases,
                Metrics = metrics,
                Clients = clients.ToList()
            };
        }

        public static Dictionary<string, long> BuildPhaseMetrics(IReadOnlyDictionary<string, long> phases)
        {
            var metrics = new Dictionary<string, long>();
            if (TryDelta(phases, ConnectTimingPhases.WhoSent, ConnectTimingPhases.HelloRx, out var discovery))
                metrics["DiscoveryMs"] = discovery;
            if (TryDelta(phases, ConnectTimingPhases.ConnectStart, ConnectTimingPhases.Connected, out var connectRtt))
                metrics["ConnectRttMs"] = connectRtt;
            if (TryDelta(phases, ConnectTimingPhases.ReverseInfoStart, ConnectTimingPhases.ReverseInfoEnd, out var reverseInfo))
                metrics["ReverseInfoMs"] = reverseInfo;
            return metrics;
        }

        private static bool TryDelta(IReadOnlyDictionary<string, long> phases, string start, string end, out long delta)
        {
            delta = 0;
            if (!phases.TryGetValue(start, out var a) || !phases.TryGetValue(end, out var b))
                return false;
            delta = b - a;
            return delta >= 0;
        }
    }
}
