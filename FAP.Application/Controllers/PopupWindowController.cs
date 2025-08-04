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
using FAP.Domain.Services;
using Fap.Foundation;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace FAP.Application.Controllers
{
    public class PopupWindowController
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Logger logger;
        private readonly List<object> windows = new List<object>();

        public PopupWindowController(IServiceProvider serviceProvider)
        {
            logger = LogManager.GetLogger("faplog");
            this.serviceProvider = serviceProvider;
        }

        public void AddWindow(object view, string title)
        {
            // Implementation for adding window
            logger.Debug($"Adding window: {title}");
            windows.Add(view);
        }

        public void Close()
        {
            // Implementation for closing all windows
            logger.Debug("Closing all popup windows");
            windows.Clear();
        }

        public void SwitchToTab(object viewModel)
        {
            // Implementation for switching to tab
            logger.Debug("Switching to tab");
        }

        public object ActiveTab { get; set; }

        public void Highlight(object viewModel)
        {
            // Implementation for highlighting tab
            logger.Debug("Highlighting tab");
        }

        public void FlashIfNotActive()
        {
            // Implementation for flashing if not active
            logger.Debug("Flashing if not active");
        }
    }
}