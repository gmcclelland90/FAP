namespace Fap.Foundation
{
    public sealed class ProcessorInfo
    {
        public int NumberOfCores { get; set; }
        public int NumberOfLogicalProcessors { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MaxClockSpeed { get; set; }
        public string Architecture { get; set; } = string.Empty;
    }

    public sealed class MemoryInfo
    {
        public long Capacity { get; set; }
        public string DeviceLocator { get; set; } = string.Empty;
        public string MemoryType { get; set; } = string.Empty;
        public int Speed { get; set; }
    }

    public sealed class VideoControllerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string AdapterRAM { get; set; } = string.Empty;
        public int VideoProcessor { get; set; }
        public int VideoMemoryType { get; set; }
        public int CurrentHorizontalResolution { get; set; }
        public int CurrentVerticalResolution { get; set; }
    }

    public sealed class DiskInfo
    {
        public string DeviceID { get; set; } = string.Empty;
        public long Size { get; set; }
        public long FreeSpace { get; set; }
        public string FileSystem { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
    }

    public sealed class NetworkAdapterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string AdapterType { get; set; } = string.Empty;
        public long Speed { get; set; }
        public string MACAddress { get; set; } = string.Empty;
    }
}


