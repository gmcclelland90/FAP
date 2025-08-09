using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Fap.Foundation
{
    public sealed class SystemSpecification
    {
        public ProcessorInfo? Processor { get; set; }
        public IEnumerable<MemoryInfo>? Memory { get; set; }
        public IEnumerable<VideoControllerInfo>? VideoControllers { get; set; }
        public IEnumerable<DiskInfo>? Disks { get; set; }
        public IEnumerable<NetworkAdapterInfo>? NetworkAdapters { get; set; }
    }

    public class ParallelWmiService
    {
        private readonly WmiService _wmi;
        private readonly ILogger<ParallelWmiService>? _logger;

        public ParallelWmiService(WmiService wmi, ILogger<ParallelWmiService>? logger = null)
        {
            _wmi = wmi;
            _logger = logger;
        }

        public async Task<SystemSpecification> GetSystemSpecificationParallelAsync()
        {
            var cpuTask = _wmi.QuerySingleAsync("SELECT NumberOfCores, NumberOfLogicalProcessors, Name, MaxClockSpeed, Architecture FROM Win32_Processor",
                o => new ProcessorInfo
                {
                    NumberOfCores = Convert.ToInt32(o["NumberOfCores"]),
                    NumberOfLogicalProcessors = Convert.ToInt32(o["NumberOfLogicalProcessors"]),
                    Name = o["Name"]?.ToString() ?? string.Empty,
                    MaxClockSpeed = Convert.ToInt32(o["MaxClockSpeed"]),
                    Architecture = o["Architecture"]?.ToString() ?? string.Empty
                });

            var memTask = _wmi.QueryMultipleAsync("SELECT Capacity, DeviceLocator, MemoryType, Speed FROM Win32_PhysicalMemory",
                o => new MemoryInfo
                {
                    Capacity = Convert.ToInt64(o["Capacity"]),
                    DeviceLocator = o["DeviceLocator"]?.ToString() ?? string.Empty,
                    MemoryType = o["MemoryType"]?.ToString() ?? string.Empty,
                    Speed = Convert.ToInt32(o["Speed"])
                });

            var gpuTask = _wmi.QueryMultipleAsync("SELECT Name, AdapterRAM, VideoProcessor, VideoMemoryType, CurrentHorizontalResolution, CurrentVerticalResolution FROM Win32_VideoController WHERE VideoProcessor IS NOT NULL",
                o => new VideoControllerInfo
                {
                    Name = o["Name"]?.ToString() ?? string.Empty,
                    AdapterRAM = o["AdapterRAM"]?.ToString() ?? string.Empty,
                    VideoProcessor = TryInt32(o["VideoProcessor"]),
                    VideoMemoryType = TryInt32(o["VideoMemoryType"]),
                    CurrentHorizontalResolution = TryInt32(o["CurrentHorizontalResolution"]),
                    CurrentVerticalResolution = TryInt32(o["CurrentVerticalResolution"])
                });

            var diskTask = _wmi.QueryMultipleAsync("SELECT DeviceID, Size, FreeSpace, FileSystem, MediaType FROM Win32_LogicalDisk WHERE DriveType = 3",
                o => new DiskInfo
                {
                    DeviceID = o["DeviceID"]?.ToString() ?? string.Empty,
                    Size = TryInt64(o["Size"]),
                    FreeSpace = TryInt64(o["FreeSpace"]),
                    FileSystem = o["FileSystem"]?.ToString() ?? string.Empty,
                    MediaType = o["MediaType"]?.ToString() ?? string.Empty
                });

            var nicTask = _wmi.QueryMultipleAsync("SELECT Name, AdapterType, Speed, MACAddress FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE",
                o => new NetworkAdapterInfo
                {
                    Name = o["Name"]?.ToString() ?? string.Empty,
                    AdapterType = o["AdapterType"]?.ToString() ?? string.Empty,
                    Speed = TryInt64(o["Speed"]),
                    MACAddress = o["MACAddress"]?.ToString() ?? string.Empty
                });

            await Task.WhenAll(cpuTask, memTask, gpuTask, diskTask, nicTask).ConfigureAwait(false);

            return new SystemSpecification
            {
                Processor = cpuTask.Result,
                Memory = memTask.Result,
                VideoControllers = gpuTask.Result,
                Disks = diskTask.Result,
                NetworkAdapters = nicTask.Result
            };
        }

        private static int TryInt32(object? value)
        {
            try { return Convert.ToInt32(value); } catch { return 0; }
        }
        private static long TryInt64(object? value)
        {
            try { return Convert.ToInt64(value); } catch { return 0; }
        }
    }
}


