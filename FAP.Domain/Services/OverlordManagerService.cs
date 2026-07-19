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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using FAP.Domain.Entities;
using FAP.Domain.Net;
using FAP.Domain.Services;
using FAP.Network.Server;
using FAP.Network.Services;
using FAP.Shared.ConnectTiming;
using Fap.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FAP.Domain.Services
{
    public class OverlordManagerService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<OverlordManagerService> logger;
        private readonly Model model;
        private readonly IConnectTimingProbe connectTiming;
        private ListenerService overlordListener = null!;
        private bool isRunning;

        public OverlordManagerService(IServiceProvider serviceProvider, Model m, ILogger<OverlordManagerService> logger,
            IConnectTimingProbe connectTiming)
        {
            model = m;
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.connectTiming = connectTiming;
        }

        public void Start()
        {
            try
            {
                // Check if already running
                if (IsOverlordActive)
                {
                    logger.LogDebug("Overlord manager is already running, skipping start");
                    return;
                }

                logger.LogDebug("Starting overlord manager");
                
                // Start the overlord server on port 40
                overlordListener = serviceProvider.GetRequiredService<IListenerServiceFactory>().Create(true);
                overlordListener.Start(40);
                
                isRunning = true;
                connectTiming.Mark(ConnectTimingPhases.OverlordBound);
                logger.LogDebug("Overlord manager started successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start overlord manager");
                try { overlordListener?.Stop(); } catch { /* ignore */ }
                overlordListener = null!;
                isRunning = false;
                throw;
            }
        }

        public void Stop()
        {
            try
            {
                logger.LogDebug("Stopping overlord manager");
                
                if (overlordListener != null)
                {
                    overlordListener.Stop();
                    overlordListener = null;
                }
                
                isRunning = false;
                logger.LogDebug("Overlord manager stopped");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error stopping overlord manager");
            }
        }

        public bool IsOverlordActive
        {
            get 
            { 
                var result = isRunning && overlordListener != null && overlordListener.IsRunning;
                return result;
            }
        }

        public void StartAndStopIfNeeded()
        {
            logger.LogDebug("Starting and stopping overlord if needed");
            
            if (!IsOverlordActive)
            {
                Start();
            }
        }
    }
}