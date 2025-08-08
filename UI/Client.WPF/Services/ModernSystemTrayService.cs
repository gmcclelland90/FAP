using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Fap.Presentation.Services
{
    public class ModernSystemTrayService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly Microsoft.Extensions.Logging.ILogger<ModernSystemTrayService> _logger;
        private bool _disposed = false;

        public event EventHandler OpenRequested;
        public event EventHandler ExitRequested;

        public ModernSystemTrayService(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ModernSystemTrayService>>();
            
            try
            {
                // Create the notify icon
                _notifyIcon = new NotifyIcon();
                
                // Load the icon
                var iconPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Images", "folder-yellow.ico");
                if (File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new Icon(iconPath);
                }
                else
                {
                                    // Fallback to embedded resource
                var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Fap.Presentation;component/Images/folder-yellow.ico"));
                    if (iconStream != null)
                    {
                        _notifyIcon.Icon = new Icon(iconStream.Stream);
                    }
                }

                _notifyIcon.Text = "FAP - File Acceleration Protocol";
                _notifyIcon.Visible = true;

                // Create context menu
                _contextMenu = new ContextMenuStrip();
                
                var openMenuItem = new ToolStripMenuItem("Open FAP");
                openMenuItem.Click += (s, e) => OpenRequested?.Invoke(this, EventArgs.Empty);
                _contextMenu.Items.Add(openMenuItem);

                _contextMenu.Items.Add(new ToolStripSeparator());

                var exitMenuItem = new ToolStripMenuItem("Exit");
                exitMenuItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
                _contextMenu.Items.Add(exitMenuItem);

                _notifyIcon.ContextMenuStrip = _contextMenu;

                // Wire up events - only respond to left clicks, not right clicks
                _notifyIcon.MouseClick += (s, e) => 
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        OpenRequested?.Invoke(this, EventArgs.Empty);
                    }
                };


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize system tray service");
            }
        }

        public void Show()
        {
            if (!_disposed)
            {
                _notifyIcon.Visible = true;
            }
        }

        public void Hide()
        {
            if (!_disposed)
            {
                _notifyIcon.Visible = false;
            }
        }

        public void ShowBalloonTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
        {
            if (!_disposed)
            {
                _notifyIcon.ShowBalloonTip(timeout, title, text, icon);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _notifyIcon?.Dispose();
                _contextMenu?.Dispose();
                _disposed = true;
            }
        }
    }
} 