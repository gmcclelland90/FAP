using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Fap.Foundation
{
    public interface IHardwareInfoService
    {
        Task<SystemSpecification> GetSystemSpecificationAsync();
        Task<ProcessorInfo?> GetProcessorInfoAsync();
        Task<IReadOnlyList<MemoryInfo>> GetMemoryInfoAsync();
        Task<IReadOnlyList<VideoControllerInfo>> GetVideoControllersAsync();
        Task<IReadOnlyList<DiskInfo>> GetDiskInfoAsync();
        Task<IReadOnlyList<NetworkAdapterInfo>> GetNetworkAdaptersAsync();
        Task<string?> GetPrimarySoundDeviceAsync();
    }

    public class HardwareInfoService : IHardwareInfoService
    {
        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

        private readonly CachedWmiService _cachedWmi;
        private readonly ILogger<HardwareInfoService>? _logger;

        public HardwareInfoService(WmiService wmiService, IMemoryCache memoryCache, ILogger<HardwareInfoService>? logger = null)
        {
            _cachedWmi = new CachedWmiService(wmiService, memoryCache, null);
            _logger = logger;
        }

        // Convenience overload to avoid requiring callers to construct an IMemoryCache
        public HardwareInfoService(WmiService wmiService, ILogger<HardwareInfoService>? logger = null)
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            _cachedWmi = new CachedWmiService(wmiService, memoryCache, null);
            _logger = logger;
        }

        public async Task<SystemSpecification> GetSystemSpecificationAsync()
        {
            var cpuTask = GetProcessorInfoAsync();
            var memTask = GetMemoryInfoAsync();
            var gpuTask = GetVideoControllersAsync();
            var diskTask = GetDiskInfoAsync();
            var nicTask = GetNetworkAdaptersAsync();

            await Task.WhenAll(cpuTask, memTask, gpuTask, diskTask, nicTask).ConfigureAwait(false);

            return new SystemSpecification
            {
                Processor = await cpuTask,
                Memory = await memTask,
                VideoControllers = await gpuTask,
                Disks = await diskTask,
                NetworkAdapters = await nicTask
            };
        }

        public Task<ProcessorInfo?> GetProcessorInfoAsync() =>
            _cachedWmi.QuerySingleWithCacheAsync(
                cacheKey: "wmi:cpu",
                query: "SELECT NumberOfCores, NumberOfLogicalProcessors, Name, MaxClockSpeed, Architecture FROM Win32_Processor",
                selector: o => new ProcessorInfo
                {
                    NumberOfCores = SafeToInt32(o["NumberOfCores"]),
                    NumberOfLogicalProcessors = SafeToInt32(o["NumberOfLogicalProcessors"]),
                    Name = o["Name"]?.ToString() ?? string.Empty,
                    MaxClockSpeed = SafeToInt32(o["MaxClockSpeed"]),
                    Architecture = o["Architecture"]?.ToString() ?? string.Empty
                },
                ttl: DefaultTtl);

        public Task<IReadOnlyList<MemoryInfo>> GetMemoryInfoAsync() =>
            _cachedWmi.QueryMultipleWithCacheAsync(
                cacheKey: "wmi:mem",
                query: "SELECT Capacity, DeviceLocator, MemoryType, Speed FROM Win32_PhysicalMemory",
                selector: o => new MemoryInfo
                {
                    Capacity = SafeToInt64(o["Capacity"]),
                    DeviceLocator = o["DeviceLocator"]?.ToString() ?? string.Empty,
                    MemoryType = o["MemoryType"]?.ToString() ?? string.Empty,
                    Speed = SafeToInt32(o["Speed"])
                },
                ttl: DefaultTtl);

        public Task<IReadOnlyList<VideoControllerInfo>> GetVideoControllersAsync() =>
            _cachedWmi.QueryMultipleWithCacheAsync(
                cacheKey: "wmi:gpu",
                query: "SELECT Name, AdapterRAM, VideoProcessor, VideoMemoryType, CurrentHorizontalResolution, CurrentVerticalResolution FROM Win32_VideoController WHERE VideoProcessor IS NOT NULL",
                selector: o => new VideoControllerInfo
                {
                    Name = o["Name"]?.ToString() ?? string.Empty,
                    AdapterRAM = o["AdapterRAM"]?.ToString() ?? string.Empty,
                    VideoProcessor = SafeToInt32(o["VideoProcessor"]),
                    VideoMemoryType = SafeToInt32(o["VideoMemoryType"]),
                    CurrentHorizontalResolution = SafeToInt32(o["CurrentHorizontalResolution"]),
                    CurrentVerticalResolution = SafeToInt32(o["CurrentVerticalResolution"])
                },
                ttl: DefaultTtl);

        public Task<IReadOnlyList<DiskInfo>> GetDiskInfoAsync() =>
            _cachedWmi.QueryMultipleWithCacheAsync(
                cacheKey: "wmi:disks",
                query: "SELECT DeviceID, Size, FreeSpace, FileSystem, MediaType FROM Win32_LogicalDisk WHERE DriveType = 3",
                selector: o => new DiskInfo
                {
                    DeviceID = o["DeviceID"]?.ToString() ?? string.Empty,
                    Size = SafeToInt64(o["Size"]),
                    FreeSpace = SafeToInt64(o["FreeSpace"]),
                    FileSystem = o["FileSystem"]?.ToString() ?? string.Empty,
                    MediaType = o["MediaType"]?.ToString() ?? string.Empty
                },
                ttl: DefaultTtl);

        public Task<IReadOnlyList<NetworkAdapterInfo>> GetNetworkAdaptersAsync() =>
            _cachedWmi.QueryMultipleWithCacheAsync(
                cacheKey: "wmi:nics",
                query: "SELECT Name, AdapterType, Speed, MACAddress FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE",
                selector: o => new NetworkAdapterInfo
                {
                    Name = o["Name"]?.ToString() ?? string.Empty,
                    AdapterType = o["AdapterType"]?.ToString() ?? string.Empty,
                    Speed = SafeToInt64(o["Speed"]),
                    MACAddress = o["MACAddress"]?.ToString() ?? string.Empty
                },
                ttl: DefaultTtl);

        // Extras used by CompareVerb
        public async Task<(string Vendor, string Product)> GetBaseBoardAsync()
        {
            var result = await _cachedWmi.QuerySingleWithCacheAsync(
                cacheKey: "wmi:baseboard",
                query: "SELECT Manufacturer, Product FROM Win32_BaseBoard",
                selector: o => (Vendor: o["Manufacturer"]?.ToString() ?? string.Empty,
                                Product: o["Product"]?.ToString() ?? string.Empty),
                ttl: DefaultTtl);
            return result;
        }

        public async Task<string> GetBiosVersionAsync()
        {
            var result = await _cachedWmi.QuerySingleWithCacheAsync(
                cacheKey: "wmi:bios",
                query: "SELECT SMBIOSBIOSVersion FROM Win32_BIOS",
                selector: o => o["SMBIOSBIOSVersion"]?.ToString() ?? string.Empty,
                ttl: DefaultTtl);
            return result ?? string.Empty;
        }

        public async Task<string> GetPrimarySoundDeviceAsync()
        {
            var result = await _cachedWmi.QuerySingleWithCacheAsync(
                cacheKey: "wmi:sound",
                query: "SELECT Name FROM Win32_SoundDevice",
                selector: o => o["Name"]?.ToString() ?? string.Empty,
                ttl: DefaultTtl);
            return result ?? string.Empty;
        }

        private static int SafeToInt32(object? value) { try { return Convert.ToInt32(value); } catch { return 0; } }
        private static long SafeToInt64(object? value) { try { return Convert.ToInt64(value); } catch { return 0; } }
    }
}


