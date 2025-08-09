#region Copyright Kayomani 2010.  Licensed under the GPLv3 (Or later version), Expand for details. Do not remove this notice.
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using System.Management;
using System.Net.NetworkInformation;
using Fap.Foundation;

namespace Fap.Foundation
{
    /// <summary>
    /// Retrives system information.  Mostly uses linqToWMI which is licenced under LGPL.
    /// </summary>
    public class SystemInfo
    {
        public void Test()
        {
            Console.WriteLine("CPU speed: " + GetCPUSpeed()); //Win32_Processor
            Console.WriteLine("CPU type: " + GetCPUType());
            Console.WriteLine("CPU cores: " + GetCPUThreads());
            Console.WriteLine("CPU threads: " + GetCPUCores());
            Console.WriteLine("CPU bits: " + GetCPUBits());
            Console.WriteLine("Mobo Brand: " + GetMoboBrand()); //Win32_BaseBoard
            Console.WriteLine("Mobo Model: " + GetMoboModel());
            Console.WriteLine("BIOS: " + GetBIOSVersion()); //Win32_BIOS
            Console.WriteLine("RAM Size: " + GetMemorySize()); //Win32_PhysicalMemory
            Console.WriteLine("GPU Model: " + GetGPUDescription());//Win32_VideoController
            Console.WriteLine("GPU Count: " + GetGPUCount());
            Console.WriteLine("GPU total memory: " + GetTotalGPUMemory());
            Console.WriteLine("Primary display height: " + GetPrimaryDisplayHeight());//Win32_VideoController
            Console.WriteLine("Primary display width: " + GetPrimaryDisplayWidth());
            Console.WriteLine("Total display height: " + GetTotalDisplayHeight());
            Console.WriteLine("Total display width: " + GetTotalDisplayWidth());
            Console.WriteLine("Hard disk size: " + GetTotalHDDSize()); //Win32_LogicalDisk
            Console.WriteLine("Hard disk free: " + GetTotalHDDFree());
            Console.WriteLine("Hard disk count: " + GetHDDCount());
            Console.WriteLine("NIC Speed: " + GetNetworkSpeed()); //Win32_NetworkAdapter
            //Console.WriteLine("NIC Sent: " + GetNetworkSent());
            // Console.WriteLine("NIC Received: " + GetNetworkReceived()); 
            Console.WriteLine("Sound card: " + GetSoundcardName()); //Win32_SoundDevice
           // Console.WriteLine("System score: " + GetSystemScore());
            Console.ReadKey();
        }


       

        public int GetCPUSpeed()
        {

            string item = RegistryHelper.GetRegistryData(Registry.LocalMachine, "HARDWARE/DESCRIPTION/System/CentralProcessor/0/~MHz");
            int i = 0;
            int.TryParse(item, out i);
            return i;
        }

        public string GetCPUType()
        {
            return RegistryHelper.GetRegistryData(Registry.LocalMachine, "HARDWARE/DESCRIPTION/System/CentralProcessor/0/ProcessorNameString");
        }

        public int GetCPUCores()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor");
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    return Convert.ToInt32(obj["NumberOfCores"]);
                }
                return 1;
            }
            catch
            {
                return 1;
            }
        }

        public int GetCPUThreads()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT NumberOfLogicalProcessors FROM Win32_Processor");
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    return Convert.ToInt32(obj["NumberOfLogicalProcessors"]);
                }
                return 1;
            }
            catch
            {
                return 1;
            }
        }

        public int GetCPUBits()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT AddressWidth FROM Win32_Processor");
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    return Convert.ToInt32(obj["AddressWidth"]);
                }
                return 1;
            }
            catch { return 1; }
        }

        public string GetMoboBrand()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_BaseBoard");
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    return obj["Manufacturer"]?.ToString() ?? string.Empty;
                }
                return string.Empty;
            }
            catch { return string.Empty; }
        }

        public string GetMoboModel()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Product FROM Win32_BaseBoard");
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    return obj["Product"]?.ToString() ?? string.Empty;
                }
                return string.Empty;
            }
            catch { return string.Empty; }
        }

        public string GetBIOSVersion()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS");
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    return obj["SMBIOSBIOSVersion"]?.ToString() ?? string.Empty;
                }
                return string.Empty;
            }
            catch { return string.Empty; }
        }

        public long GetMemorySize()
        {
            try
            {
                long total = 0;
                using var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    if (obj["Capacity"] != null)
                    {
                        total += Convert.ToInt64(obj["Capacity"]);
                    }
                }
                return total;
            }
            catch { return 0; }
        }

        public string GetGPUDescription()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController WHERE VideoProcessor IS NOT NULL");
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    return obj["Name"]?.ToString() ?? string.Empty;
                }
                return string.Empty;
            }
            catch { return string.Empty; }
        }

        public int GetGPUCount()
        {
            try
            {
                int count = 0;
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController WHERE VideoProcessor IS NOT NULL");
                using var collection = searcher.Get();
                foreach (ManagementObject _ in collection) { count++; }
                return count == 0 ? 1 : count;
            }
            catch { return 1; }
        }


        public long GetTotalGPUMemory()
        {
            try
            {
                long total = 0;
                using var searcher = new ManagementObjectSearcher("SELECT AdapterRAM FROM Win32_VideoController WHERE VideoProcessor IS NOT NULL");
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    if (obj["AdapterRAM"] != null)
                        total += Convert.ToInt64(obj["AdapterRAM"]);
                }
                return total == 0 ? 1 : total;
            }
            catch { return 1; }
        }
        public int GetPrimaryDisplayHeight()
        {
            try
            {
                return System.Windows.Forms.Screen.AllScreens.Where(s => s.Primary).FirstOrDefault().Bounds.Height;
            }
            catch { return 0; }
        }

        public int GetPrimaryDisplayWidth()
        {
            try
            {
                return System.Windows.Forms.Screen.AllScreens.Where(s => s.Primary).FirstOrDefault().Bounds.Width;
            }
            catch { return 0; }
        }

        public int GetTotalDisplayHeight()
        {
            try
            {
                var height = from s in System.Windows.Forms.Screen.AllScreens
                             select s.Bounds.Height;
                return height.Sum();
            }
            catch { return 0; }
        }

        public int GetTotalDisplayWidth()
        {
            try
            {
                var height = from s in System.Windows.Forms.Screen.AllScreens
                             select s.Bounds.Width;
                return height.Sum();
            }
            catch { return 0; }
        }

        /// <summary>
        /// Note display size is (value * 1073741824)/1000000000
        /// </summary>
        /// <returns></returns>
        public long GetTotalHDDSize()
        {
            try
            {
                long total = 0;
                using var searcher = new ManagementObjectSearcher("SELECT Size, DriveType FROM Win32_LogicalDisk WHERE DriveType = 3");
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    if (obj["Size"] != null)
                        total += Convert.ToInt64(obj["Size"]);
                }
                return total;
            }
            catch { return 0; }
        }

        public long GetTotalHDDFree()
        {
            try
            {
                long total = 0;
                using var searcher = new ManagementObjectSearcher("SELECT FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 3");
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    if (obj["FreeSpace"] != null)
                        total += Convert.ToInt64(obj["FreeSpace"]);
                }
                return total;
            }
            catch { return 0; }
        }

        public int GetHDDCount()
        {
            try
            {
                int count = 0;
                using var searcher = new ManagementObjectSearcher("SELECT DeviceID FROM Win32_LogicalDisk WHERE DriveType = 3");
                using var collection = searcher.Get();
                foreach (ManagementObject _ in collection) { count++; }
                return count;
            }
            catch { return 0; }
        }


        public string GetSoundcardName()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_SoundDevice");
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    return obj["Name"]?.ToString() ?? string.Empty;
                }
                return string.Empty;
            }
            catch { return string.Empty; }
        }


        public long GetNetworkSpeed()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var inter in interfaces)
            {
                if (inter.OperationalStatus == OperationalStatus.Up)
                    return inter.Speed;
            }
            return 0;
        }


        public long GetNetworkSent()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var inter in interfaces)
            {
                if (inter.OperationalStatus == OperationalStatus.Up)
                    return inter.GetIPv4Statistics().BytesSent;
            }
            return 0;
        }

        public long GetNetworkReceived()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var inter in interfaces)
            {
                if (inter.OperationalStatus == OperationalStatus.Up)
                {
                    var d = inter.GetIPv4Statistics();
                    return d.BytesReceived;
                }
            }
            return 0;
        }

        /*private string GetWMIInfo(string root, string query, string value)
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(root, query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    return queryObj[value];
                }
            }
            catch (ManagementException e)
            {
                return string.Empty;
            }
        }*/
    }
}
