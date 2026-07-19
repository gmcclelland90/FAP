using System.Collections.Generic;

namespace FAP.Shared.ConnectTiming
{
    /// <summary>
    /// Records phase timestamps for open → Connected latency measurement.
    /// No-op when <see cref="Enabled"/> is false.
    /// </summary>
    public interface IConnectTimingProbe
    {
        bool Enabled { get; set; }

        void Reset();

        void Mark(string phase);

        /// <summary>Milliseconds from Reset (T0) to each phase mark.</summary>
        IReadOnlyDictionary<string, long> Snapshot();

        /// <summary>Elapsed ms since Reset, or 0 if never reset.</summary>
        long ElapsedMs { get; }
    }
}
