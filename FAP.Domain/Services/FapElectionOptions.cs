namespace FAP.Domain.Services
{
    /// <summary>
    /// How long the client waits for LAN Hello discovery before self-electing an overlord.
    /// </summary>
    public sealed class FapElectionOptions
    {
        /// <summary>
        /// Milliseconds after the client listener starts to wait for an overlord Hello
        /// before self-electing. 0 = elect on the first watchdog check (validated by
        /// TimeToConnected multi_cold_start: one overlord, join does not self-elect).
        /// </summary>
        public int DiscoveryGraceMs { get; set; } = 0;

        /// <summary>
        /// Obsolete: ignored. Kept so existing config sections deserialize without error.
        /// Use <see cref="DiscoveryGraceMs"/>.
        /// </summary>
        public int DiscoveryGraceCycles { get; set; } = 2;
    }
}
