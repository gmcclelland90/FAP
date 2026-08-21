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
using FAP.Domain;
using FAP.Domain.Entities;
using FAP.Domain.Net;
using FAP.Domain.Services;
using FAP.Shared.ConnectTiming;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FAP.Application.Controllers
{
    public class WatchdogController
    {
        private readonly BufferService bufferService;
        private readonly Model model;
        private readonly OverlordManagerService overlordLauncherService;
        private readonly LANPeerFinderService peerFinder;
        private readonly SharesController shareController;
        private readonly object sync = new object();
        private readonly List<DownloadWorkerService> workers = new List<DownloadWorkerService>();
        private readonly Microsoft.Extensions.Logging.ILogger<WatchdogController> logger;
        private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
        private readonly IConnectTimingProbe connectTiming;
        private readonly FapElectionOptions electionOptions;
        private bool run;
        /// <summary>TickCount64 when client listener started; 0 until <see cref="OnClientListening"/>.</summary>
        private long discoveryStartedAt;

        public WatchdogController(Model m, SharesController s, BufferService b, OverlordManagerService o,
            LANPeerFinderService peerFinder,
            Microsoft.Extensions.Logging.ILogger<WatchdogController> logger,
            System.Net.Http.IHttpClientFactory httpClientFactory,
            IConnectTimingProbe connectTiming,
            IOptions<FapElectionOptions> electionOptions)
        {
            model = m;
            shareController = s;
            bufferService = b;
            this.logger = logger;
            overlordLauncherService = o;
            this.peerFinder = peerFinder;
            _httpClientFactory = httpClientFactory;
            this.connectTiming = connectTiming;
            this.electionOptions = electionOptions?.Value ?? new FapElectionOptions();
        }

        public void Start()
        {
            if (!run)
            {
                run = true;
                _ = System.Threading.Tasks.Task.Run(() => processCheckAsync(System.Threading.CancellationToken.None));
            }
        }

        public void Stop()
        {
            run = false;
        }

        /// <summary>
        /// Call when the client HTTP listener + WHO loop are up. Starts the discovery grace clock.
        /// </summary>
        public void OnClientListening()
        {
            if (discoveryStartedAt == 0)
                discoveryStartedAt = Environment.TickCount64;
        }

        private async System.Threading.Tasks.Task processCheckAsync(System.Threading.CancellationToken token)
        {
            int lastRun = Environment.TickCount;
            // Wall-clock cadence — runCount must NOT drive saves; discovery polls at 50ms and would
            // otherwise Save() into the user's ClientConfig after a few seconds.
            long lastShareRefreshAt = 0;
            long lastConfigSaveAt = Environment.TickCount64; // skip save on startup
            long lastGcAt = 0;

            logger.LogInformation(
                "WatchdogController started (DiscoveryGraceMs={GraceMs})",
                Math.Max(0, electionOptions.DiscoveryGraceMs));

            while (run && !token.IsCancellationRequested)
            {
                lastRun = Environment.TickCount;
                long now = Environment.TickCount64;

                // Prefer joining an existing overlord; only start one if none are discovered after grace.
                logger.LogDebug("WatchdogController: model.IsDedicated={Dedicated}, overlordLauncherService.IsOverlordActive={Active}", model.IsDedicated, overlordLauncherService.IsOverlordActive);
                TryElectAfterDiscoveryGrace();

                //Update node transfer info
                try
                {
                    model.LocalNode.DownloadSpeed = NetworkSpeedMeasurement.TotalDownload.GetSpeed();
                    model.LocalNode.UploadSpeed = NetworkSpeedMeasurement.TotalUpload.GetSpeed();
                }
                catch
                {
                }

                bufferService.Clean();

                // Share refresh every ~5 minutes
                if (lastShareRefreshAt == 0 || now - lastShareRefreshAt >= 300_000)
                {
                    lastShareRefreshAt = now;
                    shareController.RefreshShareInfo();
                }

                // Persist config/queue every ~5 minutes (not on first tick)
                if (now - lastConfigSaveAt >= 300_000)
                {
                    lastConfigSaveAt = now;
                    try
                    {
                        model.GetAntiShutdownLock();
                        model.Save();
                        model.DownloadQueue.Save();
                    }
                    catch
                    {
                    }
                    finally
                    {
                        model.ReleaseAntiShutdownLock();
                    }
                }

                //Scan for downloads
                try
                {
                    ScanForDownloads();
                }
                catch
                {
                }

                // GC every ~20 seconds
                if (now - lastGcAt >= 20_000)
                {
                    lastGcAt = now;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }

                // Poll frequently during discovery grace so short ms windows work; otherwise ~5s.
                int wait = ComputeLoopDelayMs(lastRun);
                if (wait > 0)
                    await System.Threading.Tasks.Task.Delay(wait, token);
            }
        }

        private void TryElectAfterDiscoveryGrace()
        {
            if (model.IsDedicated ||
                overlordLauncherService.IsOverlordActive ||
                model.Network.State == ConnectionState.Connected)
                return;

            // Discovery lives in LANPeerFinderService.Peers (Hello), not Network.Nodes.
            if (peerFinder.Peers.Count > 0)
                return;

            // Clock starts at client listen/WHO; do not elect during Load-only.
            if (discoveryStartedAt == 0)
                return;

            int graceMs = Math.Max(0, electionOptions.DiscoveryGraceMs);
            long elapsed = Environment.TickCount64 - discoveryStartedAt;
            if (elapsed < graceMs)
            {
                logger.LogDebug(
                    "WatchdogController: Waiting for overlord discovery (elapsedMs={Elapsed}, graceMs={Grace})",
                    elapsed, graceMs);
                return;
            }

            logger.LogInformation("WatchdogController: No overlord detected after {Grace}ms, starting overlord", graceMs);
            connectTiming.Mark(ConnectTimingPhases.ElectStart);
            // Bind race: another client may win :40 — keep running so we can join via Hello.
            try
            {
                overlordLauncherService.StartAndStopIfNeeded();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "WatchdogController: overlord start failed (likely port race); will retry or join");
            }
        }

        private int ComputeLoopDelayMs(int lastRunTick)
        {
            const int normalMs = 5000;
            const int discoveryPollMs = 50;

            bool mayElect =
                !model.IsDedicated &&
                !overlordLauncherService.IsOverlordActive &&
                model.Network.State != ConnectionState.Connected;

            if (mayElect)
            {
                // Poll until StartClient arms the clock — a 5s sleep here would floor ElectStart at ~5s.
                if (discoveryStartedAt == 0)
                    return discoveryPollMs;

                if (peerFinder.Peers.Count == 0)
                {
                    int graceMs = Math.Max(0, electionOptions.DiscoveryGraceMs);
                    long elapsed = Environment.TickCount64 - discoveryStartedAt;
                    if (elapsed < graceMs)
                    {
                        long remaining = graceMs - elapsed;
                        return (int)Math.Min(discoveryPollMs, Math.Max(1, remaining));
                    }

                    // Past grace, still no peers / not connected — retry elect soon
                    return discoveryPollMs;
                }
            }

            int wait = normalMs - (Environment.TickCount - lastRunTick);
            return wait > 0 ? wait : 0;
        }

        private void ScanForDownloads()
        {
            lock (sync)
            {
                foreach (DownloadRequest item in model.DownloadQueue.List.ToList())
                {
                    switch (item.State)
                    {
                        case DownloadRequestState.Downloaded:
                            //Remove completed items if they have some how leaked in, this should never occur.
                            model.DownloadQueue.List.Remove(item);
                            break;
                        case DownloadRequestState.Error:
                            //Set items to retry
                            if (item.NextTryTime < Environment.TickCount)
                                item.State = DownloadRequestState.None;
                            break;
                    }
                }

                var downloads =
                    from download in model.DownloadQueue.List.ToList().Where(d => d.State == DownloadRequestState.None)
                    group download by download.ClientID
                    into g
                    select new
                               {
                                   Downloads = g,
                                   ID = g.First().ClientID,
                               };

                foreach (var group in downloads)
                {
                    //Check if the client is online
                    Node? client = model.Network.Nodes.ToList().Where(p => p.ID == group.ID).FirstOrDefault();
                    if (null == client)
                        client =
                            model.Network.Nodes.ToList().Where(c => c.Nickname == group.Downloads.First().Nickname).
                                FirstOrDefault();

                    if (null != client)
                    {
                        foreach (DownloadRequest item in group.Downloads)
                        {
                            if (item.State == DownloadRequestState.None)
                            {
                                bool addedDownload = false;

                                if (workers.Where(w => w.Node == client).Count() < model.MaxDownloadsPerUser &&
                                    workers.Count < model.MaxDownloads)
                                {
                                    addedDownload = true;
                                    //Max workers not reached, add download via new worker.
                                    var worker = new DownloadWorkerService(client, model, bufferService, Microsoft.Extensions.Logging.Abstractions.NullLogger<DownloadWorkerService>.Instance, _httpClientFactory.CreateClient("FapDefault"));
                                    worker.OnWorkerFinished += worker_OnWorkerFinished;
                                    workers.Add(worker);
                                    worker.AddDownload(item);
                                    model.TransferSessions.Add(new TransferSession(worker)
                                                                   {
                                                                       Status = "Connecting..",
                                                                       User = client.Nickname,
                                                                       Size = item.Size,
                                                                       IsDownload = true
                                                                   });
                                }
                                else
                                {
                                    //Max downloaders reached, try to add to an existing queue
                                    foreach (DownloadWorkerService worker in workers.Where(w => w.Node == client))
                                    {
                                        if (!worker.IsQueueFull)
                                        {
                                            worker.AddDownload(item);
                                            addedDownload = true;
                                            break;
                                        }
                                    }
                                }

                                if (!addedDownload)
                                {
                                    //Could not place the download so skip the rest of the queue for this host
                                    break;
                                }
                            }
                        }
                    }
                }

                //Remove redundant workers
                foreach (DownloadWorkerService worker in workers.Where(w => w.IsComplete).ToList())
                {
                    worker.OnWorkerFinished -= worker_OnWorkerFinished;
                    workers.Remove(worker);
                    TransferSession? session =
                        model.TransferSessions.ToList().Where(t => t.Worker == worker).FirstOrDefault();
                    if (null != session)
                        model.TransferSessions.Remove(session);
                }
            }
        }

        private void worker_OnWorkerFinished(object? sender, EventArgs e)
        {
            ScanForDownloads();
        }
    }
}