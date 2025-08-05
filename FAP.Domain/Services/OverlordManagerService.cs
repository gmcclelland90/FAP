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
using Fap.Foundation;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace FAP.Domain.Services
{
    public class OverlordManagerService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Logger logger;
        private readonly Model model;
        private ListenerService overlordListener;
        private bool isRunning;

        public OverlordManagerService(IServiceProvider serviceProvider, Model m)
        {
            logger = LogManager.GetLogger("faplog");
            model = m;
            this.serviceProvider = serviceProvider;
        }

        public void Start()
        {
            try
            {
                // Check if already running
                if (IsOverlordActive)
                {
                    logger.Debug("Overlord manager is already running, skipping start");
                    return;
                }

                logger.Debug("Starting overlord manager");
                
                // Start the overlord server on port 40
                overlordListener = new ListenerService(serviceProvider, true);
                overlordListener.Start(40);
                
                isRunning = true;
                logger.Debug("Overlord manager started successfully");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to start overlord manager");
                throw;
            }
        }

        public void Stop()
        {
            try
            {
                logger.Debug("Stopping overlord manager");
                
                if (overlordListener != null)
                {
                    overlordListener.Stop();
                    overlordListener = null;
                }
                
                isRunning = false;
                logger.Debug("Overlord manager stopped");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error stopping overlord manager");
            }
        }

        public bool IsOverlordActive
        {
            get { return isRunning && overlordListener != null && overlordListener.IsRunning; }
        }

        public void StartAndStopIfNeeded()
        {
            logger.Debug("Starting and stopping overlord if needed");
            
            if (!IsOverlordActive)
            {
                Start();
            }
        }
    }
}