#region Copyright Kayomani 2011.  Licensed under the GPLv3 (Or later version), Expand for details. Do not remove this notice.

/**
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or any 
    later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * */

#endregion

using System;
using FAP.Domain.Entities;
using Fap.Foundation;
using FAP.Shared.Entities;
using FAP.Shared.Interfaces;

namespace FAP.Domain.Verbs
{
    public class CompareVerb : BaseVerb, FAP.Shared.Interfaces.IVerb
    {
        private static readonly object sync = new object();
        private static NetworkRequest cachedResponse = null!;
        private static long cacheTime;
        private const int CacheTtlMs = 1000 * 300; // 5 minutes
        private readonly Model model;

        // Parameterless constructor for JSON deserialization
        public CompareVerb()
        {
            model = null!; // not used on the deserialized instance
        }

        public CompareVerb(Model m)
        {
            model = m;
        }

        public CompareNode Node { set; get; } = null!;
        public bool Allowed { set; get; }

        #region IVerb Members

        public NetworkRequest CreateRequest()
        {
            var req = new NetworkRequest();
            req.Verb = "COMPARE";
            return req;
        }

        public NetworkRequest ProcessRequest(NetworkRequest r)
        {
            Allowed = !model.DisableComparision;
            if (Allowed)
            {
                var now = Environment.TickCount;
                if (cachedResponse != null && unchecked(now - cacheTime) <= CacheTtlMs)
                {
                    return cachedResponse;
                }

                lock (sync)
                {
                    now = Environment.TickCount;
                    if (cachedResponse == null || unchecked(now - cacheTime) > CacheTtlMs)
                    {
                        var si = new SystemInfo();
                        var hw = new HardwareInfoService(new WmiService());
                        Node = new CompareNode();

                        // Fire all WMI tasks concurrently, then block for results (this method is sync)
                        var cpuTask = hw.GetProcessorInfoAsync();
                        var baseBoardTask = hw.GetBaseBoardAsync();
                        var biosTask = hw.GetBiosVersionAsync();
                        var memTask = hw.GetMemoryInfoAsync();
                        var gpuTask = hw.GetVideoControllersAsync();
                        var diskTask = hw.GetDiskInfoAsync();
                        var nicTask = hw.GetNetworkAdaptersAsync();
                        var soundTask = hw.GetPrimarySoundDeviceAsync();

                        Task.Run(() => System.Threading.Tasks.Task.WhenAll(
                            cpuTask, baseBoardTask, biosTask, memTask, gpuTask, diskTask, nicTask, soundTask
                        )).GetAwaiter().GetResult();

                        var cpu = cpuTask.Result;
                        Node.SetData("COMP-CPUSpeed", si.GetCPUSpeed().ToString());
                        Node.SetData("COMP-CPUType", cpu?.Name ?? si.GetCPUType());
                        Node.SetData("COMP-CPUCores", ((cpu?.NumberOfCores ?? 0) != 0 ? cpu!.NumberOfCores : si.GetCPUCores()).ToString());
                        Node.SetData("COMP-CPUThreads", ((cpu?.NumberOfLogicalProcessors ?? 0) != 0 ? cpu!.NumberOfLogicalProcessors : si.GetCPUThreads()).ToString());
                        Node.SetData("COMP-CPUBits", si.GetCPUBits().ToString());

                        // Motherboard / BIOS
                        var board = baseBoardTask.Result;
                        var bios = biosTask.Result;
                        Node.SetData("COMP-MoboBrand", string.IsNullOrEmpty(board.Vendor) ? si.GetMoboBrand() : board.Vendor);
                        Node.SetData("COMP-MoboModel", string.IsNullOrEmpty(board.Product) ? si.GetMoboModel() : board.Product);
                        Node.SetData("COMP-BIOSVersion", string.IsNullOrEmpty(bios) ? si.GetBIOSVersion() : bios);

                        // Memory
                        long memTotal = 0; foreach (var m in memTask.Result) memTotal += m.Capacity;
                        Node.SetData("COMP-RAMSize", (memTotal != 0 ? memTotal : si.GetMemorySize()).ToString());

                        // GPU
                        var gpus = gpuTask.Result;
                        var firstGpu = System.Linq.Enumerable.FirstOrDefault(gpus);
                        Node.SetData("COMP-GPUModel", string.IsNullOrEmpty(firstGpu?.Name) ? si.GetGPUDescription() : firstGpu!.Name);
                        int gpuCount = System.Linq.Enumerable.Count(gpus);
                        Node.SetData("COMP-GPUCount", (gpuCount != 0 ? gpuCount : si.GetGPUCount()).ToString());
                        long gpuMemSum = 0; foreach (var g in gpus) { if (long.TryParse(g.AdapterRAM, out var v)) gpuMemSum += v; }
                        Node.SetData("COMP-GPUTotalMemory", (gpuMemSum != 0 ? gpuMemSum : si.GetTotalGPUMemory()).ToString());

                        // Displays (keep from SystemInfo for now)
                        Node.SetData("COMP-DisplayPrimaryHeight", si.GetPrimaryDisplayHeight().ToString());
                        Node.SetData("COMP-DisplayPrimaryWidth", si.GetPrimaryDisplayWidth().ToString());
                        Node.SetData("COMP-DisplayTotalWidth", si.GetTotalDisplayWidth().ToString());
                        Node.SetData("COMP-DisplayTotalHeight", si.GetTotalDisplayHeight().ToString());

                        // Disks
                        long diskTotal = 0, diskFree = 0; int diskCount = 0;
                        foreach (var d in diskTask.Result) { diskTotal += d.Size; diskFree += d.FreeSpace; diskCount++; }
                        Node.SetData("COMP-HDDSize", (diskTotal != 0 ? diskTotal : si.GetTotalHDDSize()).ToString());
                        Node.SetData("COMP-HDDFree", (diskFree != 0 ? diskFree : si.GetTotalHDDFree()).ToString());
                        Node.SetData("COMP-HDDCount", (diskCount != 0 ? diskCount : si.GetHDDCount()).ToString());

                        // NIC and Sound
                        long nicMax = 0; foreach (var n in nicTask.Result) nicMax = Math.Max(nicMax, n.Speed);
                        Node.SetData("COMP-NICSpeed", (nicMax != 0 ? nicMax : si.GetNetworkSpeed()).ToString());
                        var sound = soundTask.Result;
                        Node.SetData("COMP-SoundCard", string.IsNullOrEmpty(sound) ? si.GetSoundcardName() : sound);
                        cachedResponse = new NetworkRequest { Data = Serialize(this) };
                        cacheTime = now;
                    }
                    return cachedResponse;
                }
            }
            else
            {
                return new NetworkRequest {Data = Serialize(this)};
            }
        }

        public bool ReceiveResponse(NetworkRequest r)
        {
            try
            {
                var inc = Deserialise<CompareVerb>(r.Data);
                Node = inc.Node;
                Allowed = inc.Allowed;
                if (null != Node)
                    Node.Score = inc.Node.GetSystemScore();
                else
                    Node = new CompareNode();
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}