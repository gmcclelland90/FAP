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
        private Process overlordProcess;

        public OverlordManagerService(IServiceProvider serviceProvider, Model m)
        {
            logger = LogManager.GetLogger("faplog");
            model = m;
            this.serviceProvider = serviceProvider;
        }

        public void Start()
        {
            // Implementation for starting overlord manager
            logger.Debug("Overlord manager started");
        }

        public void Stop()
        {
            // Implementation for stopping overlord manager
            logger.Debug("Overlord manager stopped");
            if (overlordProcess != null && !overlordProcess.HasExited)
            {
                overlordProcess.Kill();
                overlordProcess.Dispose();
                overlordProcess = null;
            }
        }

        public bool IsOverlordActive
        {
            get { return overlordProcess != null && !overlordProcess.HasExited; }
        }

        public void StartAndStopIfNeeded()
        {
            // Implementation for starting and stopping if needed
            logger.Debug("Starting and stopping overlord if needed");
        }
    }
}