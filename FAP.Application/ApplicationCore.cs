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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Net.Http;
using FAP.Domain.Net;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Threading;
using FAP.Application.Controllers;
using FAP.Application.ViewModel;
using FAP.Application.ViewModels;
using FAP.Domain;
using FAP.Domain.Entities;
using FAP.Domain.Services;
using FAP.Domain.Verbs;
using Fap.Foundation;
using Fap.Foundation.RegistryServices;
using Fap.Foundation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FAP.Network.Entities; // For RemoteClient

namespace FAP.Application
{
    public class ApplicationCore
    {
        private readonly ConnectionController connectionController;
        private readonly InterfaceController interfaceController;
        private readonly ILogger<ApplicationCore> logger;
        // Removed NLog-based LogService
        private readonly Model model;
        private readonly OverlordManagerService overlordManagerService;
        private readonly RegisterProtocolService registerProtocolService;
        private readonly SingleInstanceService singleInstanceService;
        private readonly UpdateCheckerService updateChecker;
        private readonly IServiceProvider serviceProvider; // For resolving services that can't be injected directly
        private ListenerService client = null!;
        private CompareController compareController = null!;
        private ConversationController conversationController = null!;
        private DownloadQueueController downloadQueueController = null!;
        private MainWindowViewModel mainWindowModel = null!;
        private IPopupWindowController popupController = null!;
        private SearchController searchController = null!;
        private SettingsController settingsController = null!;
        private SharesController shareController = null!;
        private ShareInfoService shareInfo = null!;
        private TrayIconViewModel trayIcon = null!;
        private WatchdogController watchdogController = null!;

        public ApplicationCore(
            Model model,
            ILogger<ApplicationCore> logger,
            ConnectionController connectionController,
            UpdateCheckerService updateChecker,
            InterfaceController interfaceController,
            OverlordManagerService overlordManagerService,
            IServiceProvider serviceProvider)
        {
            this.model = model;
            this.logger = logger;
            // LogService removed
            this.connectionController = connectionController;
            this.updateChecker = updateChecker;
            this.interfaceController = interfaceController;
            this.overlordManagerService = overlordManagerService;
            this.serviceProvider = serviceProvider;
            
            //Note: ServicePointManager settings are deprecated in .NET 9
            //These settings no longer affect HttpClient or SslStream
            //Connection limits and other settings are now handled by HttpClient configuration
            //System.Net.ServicePointManager.MaxServicePointIdleTime = 20000000;
            singleInstanceService = new SingleInstanceService("FAP");
            registerProtocolService = new RegisterProtocolService();
        }

        public bool CheckSingleInstance()
        {
            return singleInstanceService.GetLock();
        }

        public void Exit()
        {
            _ = Task.Run(() => ShutDownAsync(null));
        }

        public void ShutDownAsync(object? param)
        {
            model.Save();
            model.DownloadQueue.Save();
            connectionController.Exit();
            watchdogController.Stop();
            //Kill local overlord if running
            overlordManagerService.Stop();
            if (null != client)
                client.Stop();

            //Kill UI
            SafeObservableStatic.Dispatcher.Invoke(DispatcherPriority.Normal,
                                                   new Action(
                                                       delegate
                                                           {
                                                               if (null != mainWindowModel)
                                                               {
                                                                   popupController.Close();
                                                                   mainWindowModel.Close();
                                                                   trayIcon.Dispose();

                                                                   model.GetShutdownLock();
                                                                   singleInstanceService.Dispose();
                                                                   System.Windows.Application.Current.Shutdown(0);
                                                               }
                                                           }
                                                       ));
        }

        public void StartGUI(bool showWindow)
        {
            trayIcon = serviceProvider.GetRequiredService<TrayIconViewModel>();
            //Tray icon
            trayIcon.Exit = new RelayCommand(Exit);
            trayIcon.Model = model;
            trayIcon.Open = new RelayCommand(ShowMainWindow);
            trayIcon.Queue = new RelayCommand(ViewQueue);
            trayIcon.Settings = new RelayCommand(Settings);
            trayIcon.Shares = new RelayCommand(EditShares);
            trayIcon.ViewShare = new RelayCommand<object?>(viewShare);
            trayIcon.Compare = new RelayCommand(Compare);
            trayIcon.OpenExternal = new RelayCommand<object?>(OpenExternal);
            trayIcon.ShowIcon = true;
            if (showWindow)
                ShowMainWindow();
            _ = System.Threading.Tasks.Task.Run(() => MainWindowUpdaterAsync(System.Threading.CancellationToken.None));
        }

        public bool Load(bool server)
        {
            model.Load();
            model.LocalNode.Host = interfaceController.CheckAddress(model.LocalNode.Host) ?? string.Empty;
            //User chose to quit rather than select an interface =s
            if (string.IsNullOrEmpty(model.LocalNode.Host))
                return false;

            model.CheckSetDefaults();

            updateChecker.Run();

            //Immediatly send model upates
            model.LocalNode.PropertyChanged += LocalNode_PropertyChanged;

            if (!server)
            {
                //Register FAP protocol
                string location = Assembly.GetCallingAssembly().Location;
                registerProtocolService.Register("fap", location, "-url \"%1\"");

                //Delete any empty folders in the incomplete folder
                RemoveEmptyFolders(model.IncompleteFolder);
            logger.LogDebug("Client started with ID: {ClientId}", model.LocalNode.ID);

                model.DownloadQueue.Load();

                shareInfo = serviceProvider.GetRequiredService<ShareInfoService>();
                shareInfo.Load();

                // Get SharesController from DI container
                shareController = serviceProvider.GetRequiredService<SharesController>();
                shareController.Initalise();
                popupController = serviceProvider.GetRequiredService<IPopupWindowController>();
                conversationController = (ConversationController) serviceProvider.GetRequiredService<IConversationController>();
                watchdogController = serviceProvider.GetRequiredService<WatchdogController>();

                // Run as a standard client by default; allow watchdog to start an overlord via election
                model.IsDedicated = false;
                watchdogController.Start();

                if (!model.DisplayedHelp)
                {
                    try { ShowQuickStart(); }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Quick Start failed; continuing without help window");
                    }
                }
            }
            return true;
        }

        private void LocalNode_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            //Update immeadiatly on user input to give the app a nicer feel
            if (e.PropertyName == "Nickname" || e.PropertyName == "Description" || e.PropertyName == "Avatar")
                _ = Task.Run(() => updateModelAsync(null));
        }

        private void updateModelAsync(object? o)
        {
            connectionController.CheckModelChanges();
        }

        public void ShowQuickStart()
        {
            model.DisplayedHelp = true;
            var helpWindow = serviceProvider.GetRequiredService<WebViewModel>();

            if (null != helpWindow)
            {
                try
                {
                    var baseDir = AppContext.BaseDirectory ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(baseDir))
                    {
                        baseDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? string.Empty) ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(baseDir))
                            baseDir = Environment.CurrentDirectory ?? string.Empty;
                    }
                    if (string.IsNullOrWhiteSpace(baseDir))
                    {
                        logger.LogWarning("Base directory is empty; skipping Quick Start help");
                        return;
                    }
                    var helpPath = Path.Combine(baseDir, "Web.Help", "help.html");
                    if (System.IO.File.Exists(helpPath))
                    {
                        helpWindow.Location = helpPath;
                        popupController.AddWindow(helpWindow.View, "Quick Start");
                    }
                    else
                    {
                        logger.LogWarning("Quick Start help not found at {Path}; skipping help window", helpPath);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to show Quick Start help; continuing without it");
                }
            }
        }

        public void AddDownloadUrlWhenConnected(string url)
        {
            _ = Task.Run(() => AddDownloadAsync(url));
        }

        private async void AddDownloadAsync(object? url)
        {
            while (model.Network.State != ConnectionState.Connected)
                await Task.Delay(250);
            await Task.Delay(2000);
            model.AddDownloadURL((url as string)!);
        }

        public void StartClient()
        {
            logger.LogDebug("ApplicationCore.StartClient: Starting client on port {Port}", model.LocalNode.Port);
            
            // Create ListenerService with IServiceProvider instead of IContainer
            client = new ListenerService(serviceProvider, false, serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ListenerService>>());
            client.Start(model.LocalNode.Port);
            connectionController.Start();
            
            logger.LogDebug("ApplicationCore.StartClient: Client started successfully");
        }

        public async Task StartOverlordServerAsync()
        {
            logger.LogDebug("ApplicationCore.StartOverlordServer: Starting dedicated overlord server");
            
            model.IsDedicated = true;
            overlordManagerService.Start();
            
            await Task.Delay(1000);
            
            logger.LogDebug("ApplicationCore.StartOverlordServer: Starting client to connect to overlord");
            StartClient();
        }

        private void ShowMainWindow()
        {
            if (null == mainWindowModel)
            {
                mainWindowModel = serviceProvider.GetRequiredService<MainWindowViewModel>();

                mainWindowModel.WindowTitle = Model.AppVersion;
                mainWindowModel.SendChatMessage = new RelayCommand(sendChatMessage);
                mainWindowModel.ViewShare = new RelayCommand<object?>(viewShare);
                mainWindowModel.EditShares = new RelayCommand(EditShares);
                mainWindowModel.Settings = new RelayCommand(Settings);
                mainWindowModel.ViewQueue = new RelayCommand(ViewQueue);
                mainWindowModel.Closing = new RelayCommand(MainWindowClosing);
                mainWindowModel.OpenExternal = new RelayCommand<object?>(OpenExternal);
                mainWindowModel.Compare = new RelayCommand(Compare);
                mainWindowModel.Chat = new RelayCommand<object?>(Chat);
                mainWindowModel.UserInfo = new RelayCommand<object?>(showUserInfo);
                mainWindowModel.Avatar = model.Avatar;
                mainWindowModel.Nickname = model.Nickname;
                mainWindowModel.Description = model.Description;
                mainWindowModel.Sessions = model.UITransferSessions;
                mainWindowModel.Node = model.LocalNode;
                mainWindowModel.Model = model;
                mainWindowModel.Search = new RelayCommand(Search);

                var f = new SafeFilteredObservingCollection<Node>(new SafeObservingCollection<Node>(model.Network.Nodes));
                f.Filter = s => s.NodeType != ClientType.Overlord;
                mainWindowModel.Peers = f;
                mainWindowModel.ChatMessages = new SafeObservingCollection<string>(model.Messages);
            }
            else
            {
                if (mainWindowModel.Visible)
                    mainWindowModel.DoFlashWindow();
                else
                    mainWindowModel.Show();
            }

            mainWindowModel.Show();
        }

        private void RemoveEmptyFolders(string path)
        {
            string[] folders = Directory.GetDirectories(path);
            foreach (string folder in folders)
                iRemoveEmptyFolders(folder);
        }

        private void iRemoveEmptyFolders(string path)
        {
            string[] folders = Directory.GetDirectories(path);
            foreach (string folder in folders)
                iRemoveEmptyFolders(folder);
            folders = Directory.GetDirectories(path);

            if (folders.Length == 0)
            {
                if (Directory.GetFiles(path).Length == 0)
                {
                    try
                    {
                        Directory.Delete(path);
                    }
                    catch
                    {
                    }
                }
            }
        }

        #region Main window Commands

        private void Search()
        {
            if (null == searchController)
            {
                searchController = serviceProvider.GetRequiredService<SearchController>();
                searchController.Initalize();
            }
            popupController.AddWindow(searchController.ViewModel.View, "Search");
        }

        private void showUserInfo(object? obj)
        {
            var n = obj as Node;
            if (null != n)
            {
                var o = serviceProvider.GetRequiredService<UserInfoViewModel>();
                o.Node = n;
                // popupController.AddWindow(o.View, "User info (" + n.Nickname + ")");
            }
        }

        private void Chat(object? o)
        {
            var peer = o as Node;
            if (null != peer)
                conversationController.CreateConversation(peer);
        }

        private void Compare()
        {
            if (null == compareController)
            {
                compareController = serviceProvider.GetRequiredService<CompareController>();
                CompareViewModel vm = compareController.Initalise();
            }
            popupController.AddWindow(compareController.ViewModel.View, "Compare");
        }

        private void OpenExternal(object? o)
        {
            var url = o as string;
            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    // Use ProcessStartInfo to properly open URLs in the default browser
                    var psi = new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to open URL: {Url}", url);
                }
            }
        }

        private void MainWindowClosing()
        {
                mainWindowModel = null!;
        }

        private void ViewQueue()
        {
            if (null == downloadQueueController)
            {
                downloadQueueController = serviceProvider.GetRequiredService<DownloadQueueController>();
                downloadQueueController.Initalise();
            }
            popupController.AddWindow(downloadQueueController.ViewModel.View, "Download Queue");
        }

        private void sendChatMessage()
        {
            switch (mainWindowModel.CurrentChatMessage)
            {
                case "/disconnect":
                    model.Messages.Add("Disconnecting from current overlord..");
                    connectionController.Disconnect();
                    break;
                default:
                    if (!string.IsNullOrEmpty(mainWindowModel.CurrentChatMessage))
                        connectionController.SendMessage(mainWindowModel.CurrentChatMessage);
                    break;
            }
            mainWindowModel.CurrentChatMessage = string.Empty;
        }

        private void viewShare(object? o)
        {
            logger.LogDebug("viewShare: Called with object={Type}", o?.GetType().Name ?? "null");
            
            var rc = o as Node;
            logger.LogDebug("viewShare: Cast to Node result={Nickname}", rc?.Nickname ?? "null");
            
            if (null != rc)
            {
                try
                {
                        logger.LogDebug("viewShare: Original node - Nickname={Nickname}, Host={Host}, ID={Id}", rc.Nickname, rc.Host, rc.ID);
                    
                    // Create BrowserController with the specific node
                    var browserViewModel = serviceProvider.GetRequiredService<BrowserViewModel>();
                    var shareInfoService = serviceProvider.GetRequiredService<ShareInfoService>();
                    
                    // Create a proper node with all required properties
                    var properNode = new Node();
                    properNode.Host = !string.IsNullOrEmpty(rc.Host) ? rc.Host : model.LocalNode.Host;
                    properNode.ID = !string.IsNullOrEmpty(rc.ID) ? rc.ID : model.LocalNode.ID;
                    properNode.Nickname = !string.IsNullOrEmpty(rc.Nickname) ? rc.Nickname : model.Nickname;
                    properNode.NodeType = rc.NodeType;
                    properNode.Online = rc.Online;
                    
                    // Copy any additional data from the original node
                    foreach (var kvp in rc.Data)
                    {
                        properNode.SetData(kvp.Key, kvp.Value);
                    }
                    
                        logger.LogDebug("viewShare: Created proper node - Nickname={Nickname}, Host={Host}, ID={Id}", properNode.Nickname, properNode.Host, properNode.ID);
                    
                    var bc = new BrowserController(browserViewModel, model, properNode, shareInfoService,
                        serviceProvider.GetRequiredService<IHttpClientFactory>(),
                        serviceProvider.GetRequiredService<ILogger<ModernHttpClient>>());
                    bc.Initalise();
                    popupController.AddWindow(bc.ViewModel.View, "View share of " + properNode.Nickname);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error creating browser controller for node: {Nickname}", rc?.Nickname ?? "unknown");
                }
            }
            else
            {
                logger.LogWarning("viewShare called with null node");
            }
        }

        private void EditShares()
        {
            popupController.AddWindow(shareController.ViewModel.View, "Edit shares");
        }

        private void Settings()
        {
            if (null == settingsController)
            {
                settingsController = serviceProvider.GetRequiredService<SettingsController>();
                settingsController.Initaize();
            }
            popupController.AddWindow(settingsController.ViewModel.View, "Settings");
        }

        #endregion

        #region Main window UI updater

        /// <summary>
        /// Bulk update the main UI if updated
        /// </summary>
        private async System.Threading.Tasks.Task MainWindowUpdaterAsync(System.Threading.CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                MainWindowViewModel? window = mainWindowModel;
                if (null != window)
                {
                    window.Dispatcher.Invoke(DispatcherPriority.Background,
                                             new Action(
                                                 delegate
                                                     {
                                                         if (null != mainWindowModel)
                                                         {
                                                             //Update status line
                                                             {
                                                                 var sbs = new StringBuilder();
                                                                 sbs.Append("Status: ");
                                                                 sbs.Append(model.Network.State);
                                                                 sbs.Append(" as ");
                                                                 sbs.Append(model.Nickname);

                                                                 if (overlordManagerService.IsOverlordActive)
                                                                     sbs.Append(" (Overlord host)");

                                                                 if (model.Network.State == ConnectionState.Connected)
                                                                 {
                                                                     if (model.Network.Overlord.Host ==
                                                                         model.LocalNode.Host)
                                                                     {
                                                                         sbs.Append(" on yourself.");
                                                                     }
                                                                     else
                                                                     {
                                                                         sbs.Append(" on ");
                                                                         Node? search =
                                                                             model.Network.Nodes.ToList().Where(
                                                                                 n =>
                                                                                 n.Host == model.Network.Overlord.Host &&
                                                                                 n.NodeType == ClientType.Client).
                                                                                 FirstOrDefault();
                                                                         if (null == search)
                                                                             sbs.Append(model.Network.Overlord.Host);
                                                                         else
                                                                             sbs.Append(search.Nickname);
                                                                     }
                                                                 }

                                                                 window.NodeStatus = sbs.ToString();
                                                                 sbs.Length = 0;
                                                                 sbs = null;
                                                             }

                                                             //Update stats line
                                                             {
                                                                 var sb = new StringBuilder();
                                                                 sb.Append("Stats: ");

                                                                 int count =
                                                                     model.Network.Nodes.Where(
                                                                         n => n.NodeType != ClientType.Overlord).Count();
                                                                 sb.Append(count);
                                                                 if (count == 1)
                                                                     sb.Append(" client sharing ");
                                                                 else
                                                                     sb.Append(" clients sharing ");
                                                                 sb.Append(
                                                                     Utility.FormatBytes(
                                                                         model.Network.Nodes.Select(p => p.ShareSize).
                                                                             Sum()));
                                                                 sb.Append(" in ");
                                                                 sb.Append(
                                                                     Utility.ConverNumberToText(
                                                                         model.Network.Nodes.Select(p => p.FileCount).
                                                                             Sum()));
                                                                 sb.Append(" files.");

                                                                 window.CurrentNetworkStatus = sb.ToString();

                                                                 sb.Length = 0;
                                                                 sb = null;
                                                             }

                                                             // Update transfers
                                                             foreach (TransferSession xfer in mainWindowModel.Sessions)
                                                             {
                                                                 if (xfer.Worker.Length == 0)
                                                                     xfer.Percent = 0;
                                                                 else
                                                                     xfer.Percent =
                                                                         (int)
                                                                         (((double) xfer.Worker.Position/
                                                                           xfer.Worker.Length)*100);
                                                                 xfer.Size = xfer.Worker.Length;
                                                                 if (!xfer.Worker.IsComplete)
                                                                     xfer.Speed = xfer.Worker.Speed;
                                                                 xfer.Status = xfer.Worker.Status;
                                                             }

                                                             //Local stats
                                                             {
                                                                 if (model.LocalNode.DownloadSpeed == 0 &&
                                                                     model.LocalNode.UploadSpeed == 0)
                                                                 {
                                                                     string t = "Local: No transfers";
                                                                     if (mainWindowModel.LocalStats != t)
                                                                         mainWindowModel.LocalStats = t;
                                                                 }
                                                                 else
                                                                 {
                                                                     var ls = new StringBuilder();
                                                                     ls.Append("Local RX/TX: ");
                                                                     ls.Append(
                                                                         Utility.ConvertNumberToTextSpeed(
                                                                             model.LocalNode.DownloadSpeed));
                                                                     ls.Append(" / ");
                                                                     ls.Append(
                                                                         Utility.ConvertNumberToTextSpeed(
                                                                             model.LocalNode.UploadSpeed));

                                                                     window.LocalStats = ls.ToString();

                                                                     ls.Length = 0;
                                                                     ls = null;
                                                                 }
                                                             }

                                                             //Global stats
                                                             {
                                                                 long upload =
                                                                     model.Network.Nodes.ToList().Select(
                                                                         s => s.DownloadSpeed).Sum();
                                                                 long download =
                                                                     model.Network.Nodes.ToList().Select(
                                                                         s => s.UploadSpeed).Sum();


                                                                 if (upload == 0 && download == 0)
                                                                 {
                                                                     string t = "Network: No transfers";
                                                                     if (mainWindowModel.GlobalStats != t)
                                                                         mainWindowModel.GlobalStats = t;
                                                                 }
                                                                 else
                                                                 {
                                                                     var gs = new StringBuilder();
                                                                     gs.Append("Global RX/TX: ");
                                                                     gs.Append(Utility.ConvertNumberToTextSpeed(download));
                                                                     gs.Append(" / ");
                                                                     gs.Append(Utility.ConvertNumberToTextSpeed(upload));

                                                                     window.GlobalStats = gs.ToString();
                                                                     gs.Length = 0;
                                                                     gs = null;
                                                                 }
                                                             }
                                                         }
                                                     }
                                                 ));
                }
                window = null!;
                try { await System.Threading.Tasks.Task.Delay(333, token); } catch { }
            }
        }

        #endregion
    }
}