using System;

namespace FAP.Network.Server
{
    public class FapWebOptions
    {
        public bool EnableCompression { get; set; } = true;
        public bool EnableCaching { get; set; } = true;
        public bool EnableRateLimiting { get; set; } = true;
        public int StaticFilesCacheSeconds { get; set; } = 86400; // 1 day
    }
}


