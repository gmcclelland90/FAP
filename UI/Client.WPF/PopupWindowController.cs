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
using FAP.Application.Controllers;
using FAP.Application.ViewModels;
using FAP.Domain.Entities;
using FAP.Domain.Services;
using Fap.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fap.Presentation
{
    public class PopupWindowController : IPopupWindowController
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Microsoft.Extensions.Logging.ILogger<PopupWindowController> logger;
        private readonly List<TabWindow> windows = new List<TabWindow>();
        private TabWindow currentWindow;

        public PopupWindowController(IServiceProvider serviceProvider)
        {
            logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PopupWindowController>>();
            this.serviceProvider = serviceProvider;
        }

        public void AddWindow(object view, string title)
        {
            try
            {
                logger.LogDebug("Adding window: {Title}", title);
                
                // Create a new TabWindow if we don't have one or if the current one is closed
                if (currentWindow == null || !currentWindow.IsLoaded)
                {
                    currentWindow = new TabWindow();
                    var popupViewModel = serviceProvider.GetRequiredService<PopupWindowViewModel>();
                    currentWindow.DataContext = popupViewModel;
                    windows.Add(currentWindow);
                    currentWindow.Show();
                }

                // Create a new tab
                var tab = new PopUpWindowTab
                {
                    Name = title,
                    Content = view
                };

                // Get the PopupWindowViewModel and add the tab
                var viewModel = currentWindow.DataContext as PopupWindowViewModel;
                if (viewModel != null)
                {
                    logger.LogDebug("Adding tab to DocumentViews. Current count: {Count}", viewModel.DocumentViews.Count);
                    viewModel.DocumentViews.Add(tab);
                    viewModel.ActiveDocumentView = tab;
                    logger.LogDebug("Added tab. New count: {Count}", viewModel.DocumentViews.Count);
                }
                else
                {
                    logger.LogError("PopupWindowViewModel is null!");
                }

                logger.LogDebug("Successfully added window: {Title}", title);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error adding window: {Title}", title);
            }
        }

        public void Close()
        {
            logger.LogDebug("Closing all popup windows");
            foreach (var window in windows.ToList())
            {
                try
                {
                    window.Close();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error closing window");
                }
            }
            windows.Clear();
            currentWindow = null;
        }

        public void SwitchToTab(object viewModel)
        {
            logger.LogDebug("Switching to tab");
            // Implementation for switching to tab
        }

        public object ActiveTab 
        { 
            get; 
            set; 
        }

        public void Highlight(object viewModel)
        {
            logger.LogDebug("Highlighting tab");
            // Implementation for highlighting tab
        }

        public void FlashIfNotActive()
        {
            logger.LogDebug("Flashing if not active");
            if (currentWindow != null && currentWindow.IsLoaded)
            {
                currentWindow.FlashIfNotActive();
            }
        }
    }
} 