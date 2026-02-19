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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using FAP.Application.ViewModels;
using FAP.Domain.Entities;
using FAP.Domain.Services;
using Fap.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FAP.Application.Controllers
{
    public class SettingsController : AsyncControllerBase
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Microsoft.Extensions.Logging.ILogger<SettingsController> logger;
        private readonly Model model;
        private SettingsViewModel viewModel;

        public SettingsController(IServiceProvider serviceProvider, Model m)
        {
            logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SettingsController>>();
            model = m;
            this.serviceProvider = serviceProvider;
        }

        public SettingsViewModel ViewModel
        {
            get { return viewModel; }
        }

        public void Initaize()
        {
            viewModel = serviceProvider.GetRequiredService<SettingsViewModel>();
            viewModel.Model = model;
            viewModel.SaveCommand = new RelayCommand(SaveCommand);
            viewModel.CancelCommand = new RelayCommand(CancelCommand);
            viewModel.ChangeAvatar = new RelayCommand(ChangeAvatar);
            viewModel.EditDownloadDir = new RelayCommand(EditDownloadDir);
        }

        private void SaveCommand()
        {
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
                string selectedFile;
                if (query.SelectFile(out selectedFile))
                {
                    // Read the image file and convert to base64
                    byte[] imageBytes = System.IO.File.ReadAllBytes(selectedFile);
                    string base64Image = Convert.ToBase64String(imageBytes);
                    // Persist avatar to disk via model setter
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
                string selectedFolder;
                if (query.SelectFolder(out selectedFolder))
                {
                    model.DownloadFolder = selectedFolder;
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