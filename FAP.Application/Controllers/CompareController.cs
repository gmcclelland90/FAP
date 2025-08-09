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
using System.Threading;
using System.Waf.Applications;
using System.Waf.Applications.Services;
using FAP.Application.ViewModels;
using FAP.Domain.Entities;
using FAP.Domain;
using FAP.Domain.Net;
using FAP.Domain.Services;
using FAP.Domain.Verbs;
using Fap.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FAP.Application.Controllers
{
    public class CompareController : AsyncControllerBase
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Microsoft.Extensions.Logging.ILogger<CompareController> logger;
        private readonly Model model;
        private CompareViewModel viewModel;

        public CompareController(IServiceProvider serviceProvider, Model m)
        {
            logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CompareController>>();
            model = m;
            this.serviceProvider = serviceProvider;
        }

        public CompareViewModel ViewModel
        {
            get { return viewModel; }
        }

        public CompareViewModel Initalise()
        {
            if (null == viewModel)
            {
                viewModel = serviceProvider.GetRequiredService<CompareViewModel>();
                viewModel.Run = new DelegateCommand(Compare);
                viewModel.Reset = new DelegateCommand(Reset);
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

            QueueWork(new DelegateCommand(() =>
            {
                try
                {
                    var peers = model.Network.Nodes
                        .ToList()
                        .Where(n => n.NodeType != ClientType.Overlord && n.Online)
                        .ToList();

                    if (peers.Count == 0)
                    {
                        viewModel.Status = "No peers online";
                        return;
                    }

                    // Clear previous results
                    viewModel.Data.Clear();

                    var maxParallel = Math.Max(2, Environment.ProcessorCount);
                    var options = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = maxParallel };

                    var startedAt = DateTime.UtcNow;
                    System.Threading.Tasks.Parallel.ForEach(peers, options, peer =>
                    {
                        try
                        {
                            var peerStart = DateTime.UtcNow;
                            var client = new Client(model.LocalNode);
                            var verb = new CompareVerb();
                            var ok = client.Execute(verb, peer, 7000);
                            if (!ok)
                            {
                                var errorNode = new CompareNode
                                {
                                    Nickname = string.IsNullOrEmpty(peer.Nickname) ? peer.Host : peer.Nickname
                                };
                                errorNode.Status = "Error";
                                errorNode.LatencyMs = (long)(DateTime.UtcNow - peerStart).TotalMilliseconds;
                                viewModel.Data.Add(errorNode);
                                return;
                            }

                            var result = verb.Node ?? new CompareNode();
                            if (string.IsNullOrEmpty(result.Nickname))
                                result.Nickname = string.IsNullOrEmpty(peer.Nickname) ? peer.Host : peer.Nickname;

                            result.Status = verb.Allowed ? "OK" : "Denied";
                            result.LatencyMs = (long)(DateTime.UtcNow - peerStart).TotalMilliseconds;
                            viewModel.Data.Add(result);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Compare failed for peer {Peer}", peer?.Nickname ?? peer?.Host ?? "unknown");
                            var errorNode = new CompareNode
                            {
                                Nickname = string.IsNullOrEmpty(peer.Nickname) ? peer.Host : peer.Nickname
                            };
                            errorNode.Status = "Error";
                            // If we reached here we still have elapsed time for the attempt
                            // Capture approximate latency for visibility
                            errorNode.LatencyMs = 0;
                            viewModel.Data.Add(errorNode);
                        }
                    });
                    var totalMs = (long)(DateTime.UtcNow - startedAt).TotalMilliseconds;
                    viewModel.Status = $"Complete in {totalMs} ms";
                }
                finally
                {
                    viewModel.EnableRun = true;
                }
            }));
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