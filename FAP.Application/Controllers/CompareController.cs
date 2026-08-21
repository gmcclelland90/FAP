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
using CommunityToolkit.Mvvm.Input;
using FAP.Application.Services;
using FAP.Application.ViewModels;
using FAP.Domain.Entities;
using Fap.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FAP.Application.Controllers
{
    public class CompareController : AsyncControllerBase
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<CompareController> logger;
        private readonly Model model;
        private readonly IPeerOrchestration peerOrchestration;
        private CompareViewModel viewModel = null!;

        public CompareController(IServiceProvider serviceProvider, Model m, IPeerOrchestration peerOrchestration)
        {
            logger = serviceProvider.GetRequiredService<ILogger<CompareController>>();
            model = m;
            this.serviceProvider = serviceProvider;
            this.peerOrchestration = peerOrchestration;
        }

        public CompareViewModel ViewModel => viewModel;

        public CompareViewModel Initalise()
        {
            if (null == viewModel)
            {
                viewModel = serviceProvider.GetRequiredService<CompareViewModel>();
                viewModel.Run = new RelayCommand(Compare);
                viewModel.Reset = new RelayCommand(Reset);
                viewModel.Data = new SafeObservable<CompareNode>();
                viewModel.Status = "Idle";
            }
            return viewModel;
        }

        private void Compare()
        {
            logger.LogDebug("Compare operation started");
            if (viewModel == null) return;
            viewModel.EnableRun = false;
            viewModel.Status = "Collecting...";

            QueueWork(_ =>
            {
                string status = "Idle";
                try
                {
                    var startedAt = DateTime.UtcNow;
                    var results = peerOrchestration.ComparePeersAsync(model).GetAwaiter().GetResult();
                    SafeObservableStatic.UiDispatcher?.Invoke(() =>
                    {
                        viewModel.Data.Clear();
                        foreach (var node in results)
                            viewModel.Data.Add(node);
                    });
                    var totalMs = (long)(DateTime.UtcNow - startedAt).TotalMilliseconds;
                    status = results.Count == 0 ? "No responses" : $"Complete in {totalMs} ms";
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Compare operation failed");
                    status = "Compare failed: " + ex.Message;
                }
                finally
                {
                    var finalStatus = status;
                    void Finish()
                    {
                        viewModel.Status = finalStatus;
                        viewModel.EnableRun = true;
                    }

                    if (SafeObservableStatic.UiDispatcher != null)
                        SafeObservableStatic.UiDispatcher.Invoke(Finish);
                    else
                        Finish();
                }
            });
        }

        private void Reset()
        {
            logger.LogDebug("Reset operation started");
            if (viewModel == null) return;
            viewModel.Data?.Clear();
            viewModel.Status = "Idle";
        }
    }
}
