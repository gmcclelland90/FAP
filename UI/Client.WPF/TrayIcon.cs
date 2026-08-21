#region Copyright Kayomani 2010.  Licensed under the GPLv3 (Or later version), Expand for details. Do not remove this notice.
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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FAP.Domain;
using FAP.Application;
using FAP.Network;
using Fap.Foundation;
using FAP.Application.ViewModel;
using FAP.Application.ViewModels;
using FAP.Application.Views;
using FAP.Domain.Entities;
using FAP.Domain.Net;

namespace Fap.Presentation
{
    public class TrayIcon : ITrayIconView
    {
        // TODO: Replace Windows Forms NotifyIcon with WPF-based solution for .NET 9
        // private System.Windows.Forms.NotifyIcon notifyIcon;
        // private System.Windows.Forms.ContextMenu contextMenu;

        private TrayIconViewModel model;

        public TrayIcon()
        {
            // TODO: Implement WPF-based tray icon for .NET 9
            // For now, we'll create a stub implementation
            // notifyIcon = new System.Windows.Forms.NotifyIcon();
            // contextMenu = new System.Windows.Forms.ContextMenu();
            // notifyIcon.ContextMenu = contextMenu;
            // Stream iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Fap.Presentation;component/Images/folder-yellow.ico")).Stream;
            // notifyIcon.Icon = new System.Drawing.Icon(iconStream);
            // contextMenu.Popup += new EventHandler(contextMenu_Popup);
            // notifyIcon.DoubleClick += new EventHandler(notifyIcon_DoubleClick);
            // notifyIcon.Click += new EventHandler(notifyIcon_Click);
            // notifyIcon.Text = "FAP";
            // notifyIcon.Visible = true;
        }

        void notifyIcon_Click(object sender, EventArgs e)
        {
            try
            {
                if (null != model)
                    model.Open.Execute(null);
            }
            catch { }
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                if (null != model)
                    model.Open.Execute(null);
            }
            catch { }
        }

        private void contextMenu_Popup(object sender, EventArgs e)
        {
            try
            {
                // TODO: Implement context menu for WPF-based tray icon
            }
            catch { }
        }

        public TrayIconViewModel Model
        {
            set { model = value; }
        }

        public bool ShowIcon
        {
            get { return true; } // TODO: Implement proper show/hide logic
            set { /* TODO: Implement show/hide logic */ }
        }

        public object DataContext
        {
            get { return model; }
            set { model = value as TrayIconViewModel; }
        }

        public void Dispose()
        {
            // TODO: Implement proper disposal for WPF-based tray icon
        }
    }
}
