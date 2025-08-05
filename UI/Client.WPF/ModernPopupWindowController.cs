using System;
using System.Collections.Generic;
using System.Linq;
using FAP.Application.Controllers;
using FAP.Application.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace Fap.Presentation
{
    public class ModernPopupWindowController : IPopupWindowController
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Logger logger;
        private readonly List<ModernTabWindow> windows = new List<ModernTabWindow>();
        private ModernTabWindow currentWindow;

        public ModernPopupWindowController(IServiceProvider serviceProvider)
        {
            logger = LogManager.GetLogger("faplog");
            this.serviceProvider = serviceProvider;
        }

        public void AddWindow(object view, string title)
        {
            try
            {
                logger.Debug($"Adding window: {title}");
                
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
                    logger.Debug($"Adding tab: {title}");
                    viewModel.AddTab(title, view);
                    logger.Debug($"Successfully added tab: {title}");
                }
                else
                {
                    logger.Error("ModernTabWindowViewModel is null!");
                }

                logger.Debug($"Successfully added window: {title}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error adding window: {title}");
            }
        }

        public void Close()
        {
            logger.Debug("Closing all popup windows");
            foreach (var window in windows.ToList())
            {
                try
                {
                    window.Close();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error closing window");
                }
            }
            windows.Clear();
            currentWindow = null;
        }

        public void SwitchToTab(object viewModel)
        {
            logger.Debug("Switching to tab");
            // Implementation for switching to tab
        }

        public object ActiveTab 
        { 
            get; 
            set; 
        }

        public void Highlight(object viewModel)
        {
            logger.Debug("Highlighting tab");
            // Implementation for highlighting tab
        }

        public void FlashIfNotActive()
        {
            logger.Debug("Flashing if not active");
            if (currentWindow != null && currentWindow.IsLoaded)
            {
                // Flash the window if it's not active
                currentWindow.Activate();
            }
        }
    }
} 