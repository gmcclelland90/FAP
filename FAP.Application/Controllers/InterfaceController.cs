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

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using CommunityToolkit.Mvvm.Input;
using FAP.Application.Services;
using FAP.Application.ViewModels;
using FAP.Domain.Entities;

namespace FAP.Application.Controllers
{
    public class InterfaceController
    {
        private readonly InterfaceSelectionViewModel vm;
        private IMessageService message;
        private bool quit;

        public InterfaceController(InterfaceSelectionViewModel v, IMessageService m)
        {
            vm = v;
            message = m;
            vm.Interfaces = new BindingList<NetInterface>(
                NetworkInterfaceCatalog.ListIPv4(includeLoopback: false).ToList());

            // Select primary interface — prefer first host address that matches a listed NIC
            try
            {
                foreach (IPAddress t in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    vm.SelectedInterface = vm.Interfaces.FirstOrDefault(s => Equals(s.Address, t))!;
                    if (null != vm.SelectedInterface)
                        break;
                }
            }
            catch
            {
                // leave SelectedInterface unset
            }

            if (vm.SelectedInterface == null && vm.Interfaces.Count > 0)
                vm.SelectedInterface = vm.Interfaces[0];

            vm.Quit = new RelayCommand(Quit);
            vm.Select = new RelayCommand(Select);
        }

        private void Quit()
        {
            quit = true;
            vm.Close();
        }

        private void Select()
        {
            if (null != vm.SelectedInterface)
                vm.Close();
        }

        public string? CheckAddress(string a)
        {
            // Loopback is always valid for local/test hosts (not listed in NIC scan).
            if (!string.IsNullOrWhiteSpace(a) &&
                IPAddress.TryParse(a, out var parsed) &&
                IPAddress.IsLoopback(parsed))
            {
                return a;
            }

            //Check to see if the passed address is still valid, if so just use it
            if (!vm.Interfaces.Any(t => string.Equals(t.Address.ToString(), a)))
            {
                // If there is exactly one non-loopback interface and there is no dedicated overlord, prefer loopback for local-only use
                // Fall back to UI selection only when truly ambiguous
                if (vm.Interfaces.Count == 1)
                {
                    // Prefer 127.0.0.1 for single-user local setups
                    return IPAddress.Loopback.ToString();
                }
                vm.ShowDialog();
                if (quit)
                    return null;
                return vm.SelectedInterface?.Address?.ToString();
            }

            return a;
        }
    }
}
