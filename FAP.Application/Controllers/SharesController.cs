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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FAP.Application.Services;
using System.Windows;
using FAP.Application.ViewModels;
using FAP.Application.Views;
using FAP.Domain.Entities;
using FAP.Domain.Services;
using Fap.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FAP.Application.Controllers
{
    public class SharesController : AsyncControllerBase
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Microsoft.Extensions.Logging.ILogger<SharesController> logger;
        private readonly Model model;
        private readonly ShareInfoService scanner;
        private QueryViewModel browser = null!;
        private SharesViewModel viewModel = null!;

        public SharesController(IServiceProvider serviceProvider, Model m)
        {
            logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SharesController>>();
            model = m;
            this.serviceProvider = serviceProvider;
            scanner = serviceProvider.GetRequiredService<ShareInfoService>();
        }

        public SharesViewModel ViewModel
        {
            get { return viewModel; }
        }

        public void Initalise()
        {
            viewModel = serviceProvider.GetRequiredService<SharesViewModel>();
            browser = serviceProvider.GetRequiredService<QueryViewModel>();
            viewModel.AddCommand = new RelayCommand(AddCommand);
            viewModel.RefreshCommand = new RelayCommand(RefreshCommand);
            viewModel.RemoveCommand = new RelayCommand(RemoveCommand);
            viewModel.RenameCommand = new RelayCommand(RenameCommand);
            viewModel.Shares = new SafeObservingCollection<Share>(model.Shares);
            RefreshClientStats();
        }

        private void RefreshClientStats()
        {
            model.LocalNode.ShareSize = model.Shares.Select(s => s.Size).Sum();
            model.LocalNode.FileCount = model.Shares.Select(s => s.FileCount).Sum();
        }

        private void AddCommand()
        {
            string folder = string.Empty;
            if (browser.SelectFolder(out folder))
            {
                if (model.Shares.Where(os => os.Path == folder).Count() > 0)
                {
                    logger.LogDebug("A share with this path already exists");
                }

                try
                {
                    //Check folder is accessible.
                    Directory.GetFiles(folder);

                    if (model.Shares.Where(sh => sh.Path == folder).Count() > 0)
                    {
                        MessageBox.Show("You have already shared this folder!");
                        return;
                    }

                    var s = new Share();
                    string name = folder;
                    if (name.Contains(Path.DirectorySeparatorChar))
                    {
                        name = name.Substring(name.LastIndexOf(Path.DirectorySeparatorChar) + 1,
                                              (name.Length - name.LastIndexOf(Path.DirectorySeparatorChar)) - 1);
                    }
                    //Check name is valid and ok
                    // Create a new MessageBox instance to avoid closed window issues
                    var messageBox = serviceProvider.GetRequiredService<IMessageBoxView>();
                    var messageBoxViewModel = new MessageBoxViewModel(messageBox);
                    messageBoxViewModel.Response = name;
                    messageBoxViewModel.Message = "What do you want to name the share?";
                    if (messageBoxViewModel.ShowDialog())
                        name = messageBoxViewModel.Response;
                    else
                        return;

                    if (name.Length > 0)
                    {
                        s.Name = name;
                        s.Path = folder;
                    logger.LogDebug("SharesController.AddCommand: Adding share '{Name}' with path '{Path}' to model.Shares", s.Name, s.Path);
                    logger.LogDebug("SharesController.AddCommand: model.Shares count before add: {Count}", model.Shares.Count);
                        model.Shares.Add(s);
                    logger.LogDebug("SharesController.AddCommand: model.Shares count after add: {Count}", model.Shares.Count);
                    logger.LogDebug("SharesController.AddCommand: viewModel.Shares count: {Count}", viewModel.Shares?.Count ?? 0);
                        _ = Task.Run(() => AsyncRefresh(s));
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Add share error");
                    serviceProvider.GetRequiredService<IMessageService>().ShowError("Failed to add share: " + e.Message);
                }
            }
        }

        private void AsyncRefresh(object o)
        {
            var s = o as Share;
            if (null != s)
            {
                logger.LogDebug("SharesController.AsyncRefresh: Starting refresh for share '{Name}' with path '{Path}'", s.Name, s.Path);
                s.Status = "Scanning..";
                Domain.Entities.FileSystem.Directory info = scanner.RefreshPath(s);
                s.Size = info.Size;
                s.FileCount = info.ItemCount;
                s.Status = string.Empty;
                s.LastRefresh = DateTime.Now;
                logger.LogDebug("SharesController.AsyncRefresh: Completed refresh for share '{Name}' - Size: {Size}, FileCount: {Count}", s.Name, s.Size, s.FileCount);
                RefreshClientStats();
            }
        }

        public void RefreshShareInfo()
        {
            foreach (var s in model.Shares)
            {
                _ = Task.Run(() => AsyncRefresh(s));
            }
        }

        private void RefreshCommand()
        {
            RefreshShareInfo();
        }

        private void RemoveCommand()
        {
            if (viewModel.SelectedShare != null)
            {
                model.Shares.Remove(viewModel.SelectedShare);
                RefreshClientStats();
            }
        }

        private void RenameCommand()
        {
            if (viewModel.SelectedShare != null)
            {
                // Create a new MessageBox instance to avoid closed window issues
                var messageBox = serviceProvider.GetRequiredService<IMessageBoxView>();
                var messageBoxViewModel = new MessageBoxViewModel(messageBox);
                messageBoxViewModel.Response = viewModel.SelectedShare.Name;
                messageBoxViewModel.Message = "What do you want to rename the share to?";
                if (messageBoxViewModel.ShowDialog())
                {
                    viewModel.SelectedShare.Name = messageBoxViewModel.Response;
                }
            }
        }
    }
}