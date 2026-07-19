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
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using FAP.Application.Services;
using FAP.Application.ViewModels;
using FAP.Domain.Entities;
using Fap.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FAP.Application.Controllers
{
    public class SettingsController : AsyncControllerBase
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<SettingsController> logger;
        private readonly Model model;
        private SettingsViewModel viewModel = null!;

        public SettingsController(IServiceProvider serviceProvider, Model m)
        {
            logger = serviceProvider.GetRequiredService<ILogger<SettingsController>>();
            model = m;
            this.serviceProvider = serviceProvider;
        }

        public SettingsViewModel ViewModel => viewModel;

        public void Initaize()
        {
            viewModel = serviceProvider.GetRequiredService<SettingsViewModel>();
            viewModel.Model = model;
            viewModel.SaveCommand = new RelayCommand(SaveCommand);
            viewModel.CancelCommand = new RelayCommand(CancelCommand);
            viewModel.ChangeAvatar = new RelayCommand(ChangeAvatar);
            viewModel.EditDownloadDir = new RelayCommand(EditDownloadDir);
            viewModel.ResetInterface = new RelayCommand(RefreshNetworkInterfaces);
            viewModel.DisplayQuickStart = new RelayCommand(ShowGettingStarted);
            RefreshNetworkInterfaces();
        }

        private void ShowGettingStarted()
        {
            serviceProvider.GetRequiredService<IGettingStartedUi>().ShowGettingStarted();
            model.DisplayedHelp = true;
            model.Save();
        }

        private void RefreshNetworkInterfaces()
        {
            var list = NetworkInterfaceCatalog.ListIPv4(includeLoopback: true).ToList();
            viewModel.AvailableInterfaces = list;
            var host = model.LocalNode?.Host;
            var match = NetworkInterfaceCatalog.FindByAddress(list, host);
            viewModel.SelectedNetworkInterface = match ?? (list.Count > 0 ? list[0] : null);
            if (viewModel.SelectedNetworkInterface?.Address != null &&
                !string.Equals(model.LocalNode.Host, viewModel.SelectedNetworkInterface.Address.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                model.LocalNode.Host = viewModel.SelectedNetworkInterface.Address.ToString();
            }
        }

        private void SaveCommand()
        {
            if (viewModel.SelectedNetworkInterface?.Address != null)
                model.LocalNode.Host = viewModel.SelectedNetworkInterface.Address.ToString();
            model.Save();
            if (viewModel.View is System.Windows.Window window)
                window.Close();
        }

        private void CancelCommand()
        {
            if (viewModel.View is System.Windows.Window window)
                window.Close();
        }

        private void ChangeAvatar()
        {
            try
            {
                var query = serviceProvider.GetRequiredService<QueryViewModel>();
                if (query.SelectImageFile(out string selectedFile))
                {
                    byte[] imageBytes = System.IO.File.ReadAllBytes(selectedFile);
                    string base64Image = Convert.ToBase64String(imageBytes);
                    model.Avatar = base64Image;
                    model.Save();
                    logger.LogDebug("Avatar changed to: {File}", selectedFile);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to change avatar");
            }
        }

        private void EditDownloadDir()
        {
            try
            {
                var query = serviceProvider.GetRequiredService<QueryViewModel>();
                if (query.SelectFolder(out string selectedFolder))
                {
                    model.DownloadFolder = selectedFolder;
                    model.IncompleteFolder = System.IO.Path.Combine(selectedFolder, "Incomplete");
                    try { System.IO.Directory.CreateDirectory(model.DownloadFolder); } catch { /* ignore */ }
                    try { System.IO.Directory.CreateDirectory(model.IncompleteFolder); } catch { /* ignore */ }
                    model.Save();
                    logger.LogDebug("Download directory changed to: {Folder}", selectedFolder);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to change download directory");
            }
        }
    }
}
