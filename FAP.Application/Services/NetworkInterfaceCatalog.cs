using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using FAP.Domain.Entities;

namespace FAP.Application.Services
{
    /// <summary>
    /// Enumerates usable IPv4 interfaces for settings and startup NIC selection.
    /// </summary>
    public static class NetworkInterfaceCatalog
    {
        public static IReadOnlyList<NetInterface> ListIPv4(bool includeLoopback)
        {
            var result = new List<NetInterface>();
            IPAddress[] localIPs;
            try
            {
                localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            }
            catch
            {
                localIPs = System.Array.Empty<IPAddress>();
            }

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || !nic.Supports(NetworkInterfaceComponent.IPv4))
                    continue;

                IPInterfaceProperties ipProps = nic.GetIPProperties();
                foreach (UnicastIPAddressInformation address in ipProps.UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    bool isLoopback = IPAddress.IsLoopback(address.Address);
                    if (isLoopback && !includeLoopback)
                        continue;

                    // Prefer host-bound addresses; still allow loopback when requested.
                    if (!isLoopback && localIPs.Length > 0 && !localIPs.Contains(address.Address))
                        continue;

                    result.Add(new NetInterface
                    {
                        Name = nic.Name,
                        Description = nic.Description,
                        Speed = nic.Speed,
                        Address = address.Address
                    });
                    break; // one IPv4 per NIC
                }
            }

            return result;
        }

        public static string FormatDisplay(NetInterface nic)
        {
            if (nic == null)
                return string.Empty;
            var label = string.IsNullOrWhiteSpace(nic.Description) ? nic.Name : nic.Description;
            var addr = nic.Address?.ToString() ?? string.Empty;
            return string.IsNullOrEmpty(addr) ? label : $"{label} — {addr}";
        }

        public static NetInterface? FindByAddress(IEnumerable<NetInterface> interfaces, string? host)
        {
            if (string.IsNullOrWhiteSpace(host) || interfaces == null)
                return null;
            return interfaces.FirstOrDefault(i =>
                i.Address != null &&
                string.Equals(i.Address.ToString(), host, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
