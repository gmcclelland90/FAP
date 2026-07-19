using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FAP.Domain.Entities;
using FAP.Shared;
using Fap.Foundation;

namespace FAP.Domain.Services
{
    public interface ICompareSpecProvider
    {
        Task<CompareNode> GetCompareNodeAsync(CancellationToken cancellationToken = default);
        Task<CompareResponseV1> GetCompareResponseV1Async(Model model, string? location = null, CancellationToken cancellationToken = default);
    }

    public sealed class CompareSpecProvider : ICompareSpecProvider
    {
        private readonly IHardwareInfoService _hardware;
        private CompareSpecsV1? _cachedSpecs;
        private long _cacheTime;
        private readonly object _sync = new();
        private const int CacheTtlMs = 1000 * 300;

        public CompareSpecProvider(IHardwareInfoService hardware)
        {
            _hardware = hardware;
        }

        public async Task<CompareNode> GetCompareNodeAsync(CancellationToken cancellationToken = default)
        {
            var specs = await GetSpecsAsync(cancellationToken).ConfigureAwait(false);
            var node = new CompareNode
            {
                CPUType = specs.Cpu.Model,
                CPUCores = specs.Cpu.Cores,
                CPUThreads = specs.Cpu.Threads,
                CPUBits = specs.Cpu.Bits,
                CPUSpeed = specs.Cpu.MaxClockMhz,
                RAMSize = specs.Memory.TotalBytes,
                GPUModel = specs.Gpu?.Model ?? "",
                GPUCount = specs.Gpu?.Count ?? 0,
                GPUTotalMemory = specs.Gpu?.TotalMemoryBytes ?? 0,
                DisplayPrimaryWidth = specs.Display.PrimaryWidth,
                DisplayPrimaryHeight = specs.Display.PrimaryHeight,
                DisplayTotalWidth = specs.Display.TotalWidth,
                DisplayTotalHeight = specs.Display.TotalHeight,
                HDDSize = specs.Storage.TotalBytes,
                HDDFree = specs.Storage.FreeBytes,
                HDDCount = specs.Storage.Count,
                NICSpeed = specs.Network.MaxLinkSpeedbps,
                SoundCard = specs.Audio?.DeviceName ?? ""
            };
            node.Score = node.GetSystemScore();
            return node;
        }

        public async Task<CompareResponseV1> GetCompareResponseV1Async(Model model, string? location = null, CancellationToken cancellationToken = default)
        {
            if (model.DisableComparision)
            {
                return new CompareResponseV1(false, "Comparisons disabled", model.Nickname ?? "", location ?? model.LocalNode?.Location ?? "", null);
            }

            var specs = await GetSpecsAsync(cancellationToken).ConfigureAwait(false);
            return new CompareResponseV1(true, null, model.Nickname ?? Environment.MachineName, location ?? model.LocalNode?.Location ?? "", specs);
        }

        private async Task<CompareSpecsV1> GetSpecsAsync(CancellationToken cancellationToken)
        {
            var now = Environment.TickCount64;
            lock (_sync)
            {
                if (_cachedSpecs != null && unchecked(now - _cacheTime) <= CacheTtlMs)
                    return _cachedSpecs;
            }

            var sys = new SystemInfo();
            var cpuTask = _hardware.GetProcessorInfoAsync();
            var memTask = _hardware.GetMemoryInfoAsync();
            var gpuTask = _hardware.GetVideoControllersAsync();
            var diskTask = _hardware.GetDiskInfoAsync();
            var nicTask = _hardware.GetNetworkAdaptersAsync();
            var soundTask = _hardware.GetPrimarySoundDeviceAsync();
            await Task.WhenAll(cpuTask, memTask, gpuTask, diskTask, nicTask, soundTask).ConfigureAwait(false);

            var cpu = await cpuTask.ConfigureAwait(false);
            var memBytes = (await memTask.ConfigureAwait(false)).Sum(m => m.Capacity);
            var gpus = await gpuTask.ConfigureAwait(false);
            var firstGpu = gpus.FirstOrDefault();
            long gpuMem = 0;
            foreach (var g in gpus)
            {
                if (long.TryParse(g.AdapterRAM, out var v)) gpuMem += v;
            }
            var disks = await diskTask.ConfigureAwait(false);
            long dTotal = 0, dFree = 0;
            int dCount = 0;
            foreach (var d in disks) { dTotal += d.Size; dFree += d.FreeSpace; dCount++; }
            var nics = await nicTask.ConfigureAwait(false);
            long link = 0;
            foreach (var n in nics) link = Math.Max(link, n.Speed);
            var sound = await soundTask.ConfigureAwait(false);

            var specs = new CompareSpecsV1(
                Cpu: new CpuInfoV1(
                    Model: cpu?.Name ?? sys.GetCPUType(),
                    Cores: cpu?.NumberOfCores != 0 ? cpu!.NumberOfCores : sys.GetCPUCores(),
                    Threads: cpu?.NumberOfLogicalProcessors != 0 ? cpu!.NumberOfLogicalProcessors : sys.GetCPUThreads(),
                    Bits: sys.GetCPUBits(),
                    MaxClockMhz: cpu?.MaxClockSpeed != 0 ? cpu!.MaxClockSpeed : sys.GetCPUSpeed()),
                Memory: new MemoryInfoV1(memBytes != 0 ? memBytes : sys.GetMemorySize()),
                Gpu: string.IsNullOrEmpty(firstGpu?.Name) ? null : new GpuInfoV1(firstGpu!.Name, gpuMem, gpus.Count),
                Display: new DisplayInfoV1(sys.GetPrimaryDisplayWidth(), sys.GetPrimaryDisplayHeight(), sys.GetTotalDisplayWidth(), sys.GetTotalDisplayHeight()),
                Storage: new StorageInfoV1(dTotal != 0 ? dTotal : sys.GetTotalHDDSize(), dFree != 0 ? dFree : sys.GetTotalHDDFree(), dCount != 0 ? dCount : sys.GetHDDCount()),
                Network: new NetworkInfoV1(link != 0 ? link : sys.GetNetworkSpeed()),
                Audio: string.IsNullOrEmpty(sound) ? null : new AudioInfoV1(sound),
                Score: 0);

            lock (_sync)
            {
                _cachedSpecs = specs;
                _cacheTime = Environment.TickCount64;
            }

            return specs;
        }
    }
}
