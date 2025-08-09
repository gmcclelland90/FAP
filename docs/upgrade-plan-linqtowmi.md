# FAP WMI Upgrade Plan: LinqToWmi → System.Management

## Overview

This plan details the migration from LinqToWmi to System.Management for .NET 9. The current LinqToWmi implementation is outdated and may not be actively maintained. System.Management provides native .NET support for WMI operations with better performance and modern APIs.

## Current State Analysis

### LinqToWmi Usage in FAP
- **Library location**: `libs/LinqToWmi/` (Core, Tests, ClassGenerator)
- **Runtime usage (app code)**:
  - `FAP.Foundation/SystemInfo.cs` (uses `LinqToWmi.Core.WMI` and `FAP.Foundation/WMI_Prototypes/*`)
  - `FAP.Domain/Verbs/CompareVerb.cs` (calls `SystemInfo` to populate `CompareNode`)
- **Generated model classes**: `FAP.Foundation/WMI_Prototypes/*.cs` (e.g., `Win32_Processor`, `Win32_PhysicalMemory`, `Win32_VideoController`, `Win32_LogicalDisk`, `Win32_BaseBoard`, `Win32_BIOS`, `Win32_SoundDevice`, `Win32_NetworkAdapter`)
- **Project references**:
  - `FAP.Foundation/FAP.Foundation.csproj` → `..\libs\LinqToWmi\LinqToWmi.Core\LinqToWmi.Core.csproj`
  - `Fap.sln` includes the `LinqToWmi.Core` project
- **Not used at runtime**: `libs/LinqToWmi/LinqToWmi.Tests/*`, `libs/LinqToWmi/LinqToWmi.ClassGenerator/*`

### Current WMI Usage Pattern
```csharp
// Current LinqToWmi usage
using LinqToWmi.Core.WMI;

public class SystemInfo
{
    public int GetCPUCores()
    {
        try
        {
            using (WmiContext context = new WmiContext(@"\\localhost"))
            {
                return (int)context.Source<Win32_Processor>().First().NumberOfCores;
            }
        }
        catch
        {
            return 1;
        }
    }
    
    public long GetMemorySize()
    {
        try
        {
            using (WmiContext context = new WmiContext(@"\\localhost"))
            {
                return context.Source<Win32_PhysicalMemory>()
                    .Sum(memory => (long)memory.Capacity);
            }
        }
        catch
        {
            return 0;
        }
    }
}
```

## Target State

### System.Management Benefits
- ✅ **Built-in**: Native .NET support
- ✅ **Performance**: Better performance than LinqToWmi
- ✅ **Modern APIs**: Async support, better error handling
- ✅ **Documentation**: Extensive Microsoft documentation
- ✅ **Maintenance**: Actively maintained by Microsoft
- ✅ **Integration**: Native .NET 9 integration

### Target WMI Usage Pattern
```csharp
// Target System.Management usage
using System.Management;

public class SystemInfo
{
    public async Task<int> GetCPUCoresAsync()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor");
            using var collection = await Task.Run(() => searcher.Get());
            
            foreach (ManagementObject obj in collection)
            {
                return Convert.ToInt32(obj["NumberOfCores"]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get CPU cores, defaulting to 1");
        }
        
        return 1;
    }
    
    public async Task<long> GetMemorySizeAsync()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
            using var collection = await Task.Run(() => searcher.Get());
            
            long totalMemory = 0;
            foreach (ManagementObject obj in collection)
            {
                totalMemory += Convert.ToInt64(obj["Capacity"]);
            }
            
            return totalMemory;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get memory size");
        }
        
        return 0;
    }
}
```

## Migration Strategy

### Acceptance criteria
- `COMPARE` verb still returns the same key set (COMP-*) with sane values on Windows.
- No project references to `LinqToWmi.*` remain; `Fap.sln` does not include `LinqToWmi.Core`.
- Folder `FAP.Foundation/WMI_Prototypes/` is removed.
- Build on `net9.0-windows` succeeds without `libs/LinqToWmi`.

### Phase 1: Foundation Setup (Week 1)

#### 1.1 Add System.Management Package
```xml
<ItemGroup>
  <PackageReference Include="System.Management" Version="9.0.0" />
</ItemGroup>
```

Targets:
- Add to `FAP.Foundation` (SystemInfo replacement lives here)
- Optionally add to `FAP.Domain` only if you plan to call WMI directly from verbs (not recommended)

#### 1.2 Create WMI Service
```csharp
// WmiService.cs
using System.Management;
using Microsoft.Extensions.Logging;

public class WmiService
{
    private readonly ILogger<WmiService> _logger;
    
    public WmiService(ILogger<WmiService> logger)
    {
        _logger = logger;
    }
    
    public async Task<T> QuerySingleAsync<T>(string query, Func<ManagementObject, T> selector)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            using var collection = await Task.Run(() => searcher.Get());
            
            foreach (ManagementObject obj in collection)
            {
                return selector(obj);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WMI query failed: {Query}", query);
        }
        
        return default(T);
    }
    
    public async Task<IEnumerable<T>> QueryMultipleAsync<T>(string query, Func<ManagementObject, T> selector)
    {
        var results = new List<T>();
        
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            using var collection = await Task.Run(() => searcher.Get());
            
            foreach (ManagementObject obj in collection)
            {
                results.Add(selector(obj));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WMI query failed: {Query}", query);
        }
        
        return results;
    }
}
```

#### 1.3 Create Hardware Information Models
```csharp
// HardwareModels.cs
public class ProcessorInfo
{
    public int NumberOfCores { get; set; }
    public int NumberOfLogicalProcessors { get; set; }
    public string Name { get; set; }
    public int MaxClockSpeed { get; set; }
    public string Architecture { get; set; }
}

public class MemoryInfo
{
    public long Capacity { get; set; }
    public string DeviceLocator { get; set; }
    public string MemoryType { get; set; }
    public int Speed { get; set; }
}

public class VideoControllerInfo
{
    public string Name { get; set; }
    public string AdapterRAM { get; set; }
    public int VideoProcessor { get; set; }
    public int VideoMemoryType { get; set; }
    public int CurrentHorizontalResolution { get; set; }
    public int CurrentVerticalResolution { get; set; }
}

public class DiskInfo
{
    public string DeviceID { get; set; }
    public long Size { get; set; }
    public long FreeSpace { get; set; }
    public string FileSystem { get; set; }
    public string MediaType { get; set; }
}

public class NetworkAdapterInfo
{
    public string Name { get; set; }
    public string AdapterType { get; set; }
    public long Speed { get; set; }
    public string MACAddress { get; set; }
    public string[] IPAddresses { get; set; }
}
```
#### 1.4 Inventory and plan replacements
- Replace `FAP.Foundation/SystemInfo.cs` methods that rely on `WmiContext` with `System.Management` equivalents.
- Remove usage of `FAP.Foundation/WMI_Prototypes/*` types; query WMI directly and project into simple models.
- Keep existing public surface of `SystemInfo` (for `CompareVerb`) to minimize churn.

### Phase 2: System Information Migration (Week 2)

#### 2.1 Update SystemInfo Class
Notes:
- Keep method names and return types to avoid changes in `CompareVerb`.
- Use `ManagementObjectSearcher` with simple SELECT statements; wrap in `Task.Run` if keeping async patterns.
```csharp
// SystemInfo.cs
using System.Management;
using Microsoft.Extensions.Logging;

public class SystemInfo
{
    private readonly WmiService _wmiService;
    private readonly ILogger<SystemInfo> _logger;
    
    public SystemInfo(WmiService wmiService, ILogger<SystemInfo> logger)
    {
        _wmiService = wmiService;
        _logger = logger;
    }
    
    public async Task<int> GetCPUCoresAsync()
    {
        var processor = await _wmiService.QuerySingleAsync(
            "SELECT NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor",
            obj => new ProcessorInfo
            {
                NumberOfCores = Convert.ToInt32(obj["NumberOfCores"]),
                NumberOfLogicalProcessors = Convert.ToInt32(obj["NumberOfLogicalProcessors"])
            });
        
        return processor?.NumberOfCores ?? 1;
    }
    
    public async Task<int> GetCPUThreadsAsync()
    {
        var processor = await _wmiService.QuerySingleAsync(
            "SELECT NumberOfLogicalProcessors FROM Win32_Processor",
            obj => new ProcessorInfo
            {
                NumberOfLogicalProcessors = Convert.ToInt32(obj["NumberOfLogicalProcessors"])
            });
        
        return processor?.NumberOfLogicalProcessors ?? 1;
    }
    
    public async Task<long> GetMemorySizeAsync()
    {
        var memories = await _wmiService.QueryMultipleAsync(
            "SELECT Capacity FROM Win32_PhysicalMemory",
            obj => new MemoryInfo
            {
                Capacity = Convert.ToInt64(obj["Capacity"])
            });
        
        return memories.Sum(m => m.Capacity);
    }
    
    public async Task<string> GetGPUDescriptionAsync()
    {
        var videoController = await _wmiService.QuerySingleAsync(
            "SELECT Name, AdapterRAM FROM Win32_VideoController WHERE VideoProcessor IS NOT NULL",
            obj => new VideoControllerInfo
            {
                Name = obj["Name"]?.ToString(),
                AdapterRAM = obj["AdapterRAM"]?.ToString()
            });
        
        return videoController?.Name ?? "Unknown";
    }
    
    public async Task<long> GetTotalGPUMemoryAsync()
    {
        var videoControllers = await _wmiService.QueryMultipleAsync(
            "SELECT AdapterRAM FROM Win32_VideoController WHERE VideoProcessor IS NOT NULL",
            obj => new VideoControllerInfo
            {
                AdapterRAM = obj["AdapterRAM"]?.ToString()
            });
        
        long totalMemory = 0;
        foreach (var controller in videoControllers)
        {
            if (long.TryParse(controller.AdapterRAM, out long memory))
            {
                totalMemory += memory;
            }
        }
        
        return totalMemory;
    }
    
    public async Task<long> GetTotalHDDSizeAsync()
    {
        var disks = await _wmiService.QueryMultipleAsync(
            "SELECT Size FROM Win32_LogicalDisk WHERE DriveType = 3",
            obj => new DiskInfo
            {
                Size = Convert.ToInt64(obj["Size"])
            });
        
        return disks.Sum(d => d.Size);
    }
    
    public async Task<long> GetTotalHDDFreeAsync()
    {
        var disks = await _wmiService.QueryMultipleAsync(
            "SELECT FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 3",
            obj => new DiskInfo
            {
                FreeSpace = Convert.ToInt64(obj["FreeSpace"])
            });
        
        return disks.Sum(d => d.FreeSpace);
    }
    
    public async Task<long> GetNetworkSpeedAsync()
    {
        var adapters = await _wmiService.QueryMultipleAsync(
            "SELECT Speed FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE",
            obj => new NetworkAdapterInfo
            {
                Speed = Convert.ToInt64(obj["Speed"])
            });
        
        return adapters.Max(a => a.Speed);
    }
}
```

#### 2.2 Create Async Hardware Information Service
Integration:
- `SystemInfo` can delegate to `HardwareInfoService` to centralize logic.
```csharp
// HardwareInfoService.cs
public class HardwareInfoService
{
    private readonly WmiService _wmiService;
    private readonly ILogger<HardwareInfoService> _logger;
    
    public HardwareInfoService(WmiService wmiService, ILogger<HardwareInfoService> logger)
    {
        _wmiService = wmiService;
        _logger = logger;
    }
    
    public async Task<SystemSpecification> GetSystemSpecificationAsync()
    {
        var tasks = new[]
        {
            GetProcessorInfoAsync(),
            GetMemoryInfoAsync(),
            GetVideoControllersAsync(),
            GetDiskInfoAsync(),
            GetNetworkAdaptersAsync()
        };
        
        await Task.WhenAll(tasks);
        
        return new SystemSpecification
        {
            Processor = await tasks[0],
            Memory = await tasks[1],
            VideoControllers = await tasks[2],
            Disks = await tasks[3],
            NetworkAdapters = await tasks[4]
        };
    }
    
    private async Task<ProcessorInfo> GetProcessorInfoAsync()
    {
        return await _wmiService.QuerySingleAsync(
            "SELECT NumberOfCores, NumberOfLogicalProcessors, Name, MaxClockSpeed, Architecture FROM Win32_Processor",
            obj => new ProcessorInfo
            {
                NumberOfCores = Convert.ToInt32(obj["NumberOfCores"]),
                NumberOfLogicalProcessors = Convert.ToInt32(obj["NumberOfLogicalProcessors"]),
                Name = obj["Name"]?.ToString(),
                MaxClockSpeed = Convert.ToInt32(obj["MaxClockSpeed"]),
                Architecture = obj["Architecture"]?.ToString()
            });
    }
    
    private async Task<IEnumerable<MemoryInfo>> GetMemoryInfoAsync()
    {
        return await _wmiService.QueryMultipleAsync(
            "SELECT Capacity, DeviceLocator, MemoryType, Speed FROM Win32_PhysicalMemory",
            obj => new MemoryInfo
            {
                Capacity = Convert.ToInt64(obj["Capacity"]),
                DeviceLocator = obj["DeviceLocator"]?.ToString(),
                MemoryType = obj["MemoryType"]?.ToString(),
                Speed = Convert.ToInt32(obj["Speed"])
            });
    }
    
    private async Task<IEnumerable<VideoControllerInfo>> GetVideoControllersAsync()
    {
        return await _wmiService.QueryMultipleAsync(
            "SELECT Name, AdapterRAM, VideoProcessor, VideoMemoryType, CurrentHorizontalResolution, CurrentVerticalResolution FROM Win32_VideoController WHERE VideoProcessor IS NOT NULL",
            obj => new VideoControllerInfo
            {
                Name = obj["Name"]?.ToString(),
                AdapterRAM = obj["AdapterRAM"]?.ToString(),
                VideoProcessor = Convert.ToInt32(obj["VideoProcessor"]),
                VideoMemoryType = Convert.ToInt32(obj["VideoMemoryType"]),
                CurrentHorizontalResolution = Convert.ToInt32(obj["CurrentHorizontalResolution"]),
                CurrentVerticalResolution = Convert.ToInt32(obj["CurrentVerticalResolution"])
            });
    }
    
    private async Task<IEnumerable<DiskInfo>> GetDiskInfoAsync()
    {
        return await _wmiService.QueryMultipleAsync(
            "SELECT DeviceID, Size, FreeSpace, FileSystem, MediaType FROM Win32_LogicalDisk WHERE DriveType = 3",
            obj => new DiskInfo
            {
                DeviceID = obj["DeviceID"]?.ToString(),
                Size = Convert.ToInt64(obj["Size"]),
                FreeSpace = Convert.ToInt64(obj["FreeSpace"]),
                FileSystem = obj["FileSystem"]?.ToString(),
                MediaType = obj["MediaType"]?.ToString()
            });
    }
    
    private async Task<IEnumerable<NetworkAdapterInfo>> GetNetworkAdaptersAsync()
    {
        return await _wmiService.QueryMultipleAsync(
            "SELECT Name, AdapterType, Speed, MACAddress FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE",
            obj => new NetworkAdapterInfo
            {
                Name = obj["Name"]?.ToString(),
                AdapterType = obj["AdapterType"]?.ToString(),
                Speed = Convert.ToInt64(obj["Speed"]),
                MACAddress = obj["MACAddress"]?.ToString()
            });
    }
}
```

### Phase 3: Performance Optimization (Week 3)

#### 3.1 Add Caching
```csharp
// CachedWmiService.cs
public class CachedWmiService
{
    private readonly WmiService _wmiService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedWmiService> _logger;
    
    public CachedWmiService(WmiService wmiService, IMemoryCache cache, ILogger<CachedWmiService> logger)
    {
        _wmiService = wmiService;
        _cache = cache;
        _logger = logger;
    }
    
    public async Task<T> QuerySingleWithCacheAsync<T>(string cacheKey, string query, Func<ManagementObject, T> selector, TimeSpan cacheDuration)
    {
        if (_cache.TryGetValue(cacheKey, out T cachedValue))
        {
            return cachedValue;
        }
        
        var result = await _wmiService.QuerySingleAsync(query, selector);
        
        if (result != null)
        {
            _cache.Set(cacheKey, result, cacheDuration);
        }
        
        return result;
    }
    
    public async Task<IEnumerable<T>> QueryMultipleWithCacheAsync<T>(string cacheKey, string query, Func<ManagementObject, T> selector, TimeSpan cacheDuration)
    {
        if (_cache.TryGetValue(cacheKey, out IEnumerable<T> cachedValue))
        {
            return cachedValue;
        }
        
        var result = await _wmiService.QueryMultipleAsync(query, selector);
        
        if (result.Any())
        {
            _cache.Set(cacheKey, result, cacheDuration);
        }
        
        return result;
    }
}
```

#### 3.2 Add Parallel Processing
```csharp
// ParallelWmiService.cs
public class ParallelWmiService
{
    private readonly WmiService _wmiService;
    private readonly ILogger<ParallelWmiService> _logger;
    
    public ParallelWmiService(WmiService wmiService, ILogger<ParallelWmiService> logger)
    {
        _wmiService = wmiService;
        _logger = logger;
    }
    
    public async Task<SystemSpecification> GetSystemSpecificationParallelAsync()
    {
        var tasks = new[]
        {
            GetProcessorInfoAsync(),
            GetMemoryInfoAsync(),
            GetVideoControllersAsync(),
            GetDiskInfoAsync(),
            GetNetworkAdaptersAsync()
        };
        
        var results = await Task.WhenAll(tasks);
        
        return new SystemSpecification
        {
            Processor = results[0],
            Memory = results[1],
            VideoControllers = results[2],
            Disks = results[3],
            NetworkAdapters = results[4]
        };
    }
}
```

### Phase 4: Error Handling and Fallbacks (Week 4)

#### 4.1 Add Fallback Mechanisms
```csharp
// FallbackSystemInfo.cs
public class FallbackSystemInfo
{
    private readonly WmiService _wmiService;
    private readonly ILogger<FallbackSystemInfo> _logger;
    
    public FallbackSystemInfo(WmiService wmiService, ILogger<FallbackSystemInfo> logger)
    {
        _wmiService = wmiService;
        _logger = logger;
    }
    
    public async Task<int> GetCPUCoresWithFallbackAsync()
    {
        try
        {
            return await _wmiService.QuerySingleAsync(
                "SELECT NumberOfCores FROM Win32_Processor",
                obj => Convert.ToInt32(obj["NumberOfCores"]));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WMI query failed, using fallback method");
            
            // Fallback to Environment.ProcessorCount
            return Environment.ProcessorCount;
        }
    }
    
    public async Task<long> GetMemorySizeWithFallbackAsync()
    {
        try
        {
            var memories = await _wmiService.QueryMultipleAsync(
                "SELECT Capacity FROM Win32_PhysicalMemory",
                obj => Convert.ToInt64(obj["Capacity"]));
            
            return memories.Sum();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WMI query failed, using fallback method");
            
            // Fallback to GC.GetTotalMemory
            return GC.GetTotalMemory(false);
        }
    }
}
```

## Migration Checklist

### Foundation
- [ ] Add System.Management package to `FAP.Foundation`
- [ ] Create `WmiService`
- [ ] Create hardware information models
- [ ] Update project references (prepare to remove LinqToWmi.Core)

### System Information Migration
- [ ] Update `SystemInfo` to use `System.Management`
- [ ] Create `HardwareInfoService`
- [ ] Migrate CPU methods (cores, threads, bits, speed)
- [ ] Migrate memory size method
- [ ] Migrate GPU methods (model, count, total memory, resolutions)
- [ ] Migrate disk methods (total size, free, count)
- [ ] Migrate NIC speed (keep existing `NetworkInterface` usage)
- [ ] Validate `CompareVerb` outputs unchanged key set

### Performance Optimization
- [ ] Add `CachedWmiService`
- [ ] Add `ParallelWmiService`
- [ ] Implement caching strategies
- [ ] Add parallel processing
- [ ] Test performance improvements

### Error Handling
- [ ] Add `FallbackSystemInfo`
- [ ] Implement fallback mechanisms
- [ ] Add comprehensive error handling
- [ ] Test error scenarios

### Cleanup
- [ ] Remove `using LinqToWmi.Core.WMI;` from all files
- [ ] Delete `FAP.Foundation/WMI_Prototypes/*`
- [ ] Remove project reference in `FAP.Foundation.csproj` to `libs/LinqToWmi/LinqToWmi.Core`
- [ ] Remove `LinqToWmi.Core` from `Fap.sln`
- [ ] Delete `libs/LinqToWmi/` (Core, Tests, ClassGenerator)
- [ ] Test all WMI functionality and `COMPARE` verb end-to-end
- [ ] Performance testing

## Risk Assessment

### High Risk
- **Query Compatibility**: Different WMI query syntax
- **Performance Impact**: Initial migration may affect performance
- **Error Handling**: Different error handling behavior

### Medium Risk
- **Data Format**: Different data format from WMI queries
- **Async Operations**: New async patterns
- **Caching**: New caching mechanisms

### Low Risk
- **Package Dependencies**: Standard .NET package
- **API Compatibility**: Well-documented APIs
- **Migration Path**: Clear migration path available

## Benefits

### Performance
- **Better Performance**: System.Management is more efficient than LinqToWmi
- **Async Support**: Native async operations
- **Caching**: Built-in caching capabilities
- **Parallel Processing**: Better parallel query support

### Features
- **Built-in**: Native .NET support
- **Modern APIs**: Async, better error handling
- **Documentation**: Extensive Microsoft documentation
- **Integration**: Native .NET 9 integration

### Maintenance
- **Better Support**: Microsoft-backed
- **Future-Proof**: Aligned with .NET roadmap
- **Fewer Dependencies**: Standard .NET package
- **Documentation**: Extensive documentation

## Testing Strategy

### Unit Tests
- [ ] Test WmiService
- [ ] Test hardware information models
- [ ] Test async operations
- [ ] Test error handling

### Integration Tests
- [ ] Test system information gathering
- [ ] Test hardware queries
- [ ] Test caching functionality
- [ ] Test fallback mechanisms

### Performance Tests
- [ ] Measure WMI query performance
- [ ] Compare with LinqToWmi performance
- [ ] Test memory usage
- [ ] Test async performance

### Compatibility Tests
- [ ] Validate `COMPARE` verb parity with legacy values
- [ ] Test with existing system information
- [ ] Test with different Windows versions
- [ ] Test backward compatibility
- [ ] Test data migration

## Migration Timeline

### Week 1: Foundation
- Add System.Management package
- Create WmiService
- Create hardware information models
- Set up basic infrastructure

### Week 2: System Information
- Migrate SystemInfo class
- Create HardwareInfoService
- Migrate all hardware query methods
- Test basic functionality

### Week 3: Performance
- Add CachedWmiService
- Add ParallelWmiService
- Implement caching strategies
- Performance optimization

### Week 4: Error Handling
- Add FallbackSystemInfo
- Implement fallback mechanisms
- Comprehensive error handling
- Final testing and cleanup
