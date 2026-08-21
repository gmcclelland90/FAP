using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FAP.Shared.ConnectTiming
{
    public sealed class ConnectTimingProbe : IConnectTimingProbe
    {
        private readonly object _sync = new();
        private readonly ConcurrentDictionary<string, long> _phases = new();
        private Stopwatch? _sw;

        public bool Enabled { get; set; }

        public long ElapsedMs
        {
            get
            {
                lock (_sync)
                    return _sw?.ElapsedMilliseconds ?? 0;
            }
        }

        public void Reset()
        {
            lock (_sync)
            {
                _phases.Clear();
                _sw = Stopwatch.StartNew();
            }
        }

        public void Mark(string phase)
        {
            if (!Enabled || string.IsNullOrEmpty(phase))
                return;

            long ms;
            lock (_sync)
            {
                if (_sw == null)
                    _sw = Stopwatch.StartNew();
                ms = _sw.ElapsedMilliseconds;
            }

            // Keep first mark for each phase name
            _phases.TryAdd(phase, ms);
        }

        public IReadOnlyDictionary<string, long> Snapshot()
        {
            return _phases.OrderBy(kv => kv.Value)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }
    }
}
