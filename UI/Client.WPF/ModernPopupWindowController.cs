using System;
using System.Collections.Generic;
using System.Linq;
using FAP.Application.Controllers;
using FAP.Application.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fap.Presentation
{
    public class ModernPopupWindowController : IPopupWindowController
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Microsoft.Extensions.Logging.ILogger<ModernPopupWindowController> logger;
        private readonly List<ModernTabWindow> windows = new List<ModernTabWindow>();
        private ModernTabWindow currentWindow;

        public ModernPopupWindowController(IServiceProvider serviceProvider)
        {
            logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ModernPopupWindowController>>();
            this.serviceProvider = serviceProvider;
        }

        public void AddWindow(object view, string title)
        {
            try
            {
                // Create a new TabWindow if we don't have one or if the current one is closed
                if (currentWindow == null || !currentWindow.IsLoaded)
                {
                    currentWindow = new ModernTabWindow();
                    var tabWindowViewModel = new ModernTabWindowViewModel();
                    currentWindow.DataContext = tabWindowViewModel;
                    windows.Add(currentWindow);
                    currentWindow.Show();
                }

                // Get the ViewModel and add the tab
                var viewModel = currentWindow.DataContext as ModernTabWindowViewModel;
                if (viewModel != null)
                {
                    viewModel.AddTab(title, view);
                }
                else
                {
                    logger.LogError("ModernTabWindowViewModel is null!");
                }
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
                // Flash the window if it's not active
                currentWindow.Activate();
            }
        }
    }
} 