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
using FAP.Application.Controllers;
using FAP.Application.Services;
using FAP.Application.ViewModel;
using FAP.Application.ViewModels;
using FAP.Domain;
using FAP.Domain.Entities;
using FAP.Domain.Services;
using FAP.Domain.Verbs;
using Fap.Foundation;
using Fap.Foundation.Hosting;
using Fap.Foundation.RegistryServices;
using Fap.Foundation.Services;
using Fap.Foundation.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FAP.Network.Entities; // For RemoteClient
using FAP.Shared.ConnectTiming;

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
        private SingleInstanceService? singleInstanceService;
        private readonly UpdateCheckerService updateChecker;
        private readonly IServiceProvider serviceProvider; // For resolving services that can't be injected directly
        private readonly IAppLifetime appLifetime;
        private readonly IConnectTimingProbe connectTiming;
        private ListenerService client = null!;
        private CompareController compareController = null!;
        private IConversationController conversationController = null!;
        private DownloadQueueController downloadQueueController = null!;
        private MainWindowViewModel mainWindowModel = null!;
        private SearchController searchController = null!;
        private SettingsController settingsController = null!;
        private SharesController shareController = null!;
        private ShareInfoService shareInfo = null!;
        private TrayIconViewModel trayIcon = null!;
        private WatchdogController watchdogController = null!;
        private IShellNavigation shellNavigation = null!;

        public ApplicationCore(
            Model model,
            ILogger<ApplicationCore> logger,
            ConnectionController connectionController,
            UpdateCheckerService updateChecker,
            InterfaceController interfaceController,
            OverlordManagerService overlordManagerService,
            IServiceProvider serviceProvider,
            IAppLifetime appLifetime,
            IConnectTimingProbe connectTiming)
        {
            this.model = model;
            this.logger = logger;
            // LogService removed
            this.connectionController = connectionController;
            this.updateChecker = updateChecker;
            this.interfaceController = interfaceController;
            this.overlordManagerService = overlordManagerService;
            this.serviceProvider = serviceProvider;
            this.appLifetime = appLifetime;
            this.connectTiming = connectTiming;
            
            //Note: ServicePointManager settings are deprecated in .NET 9
            //These settings no longer affect HttpClient or SslStream
            //Connection limits and other settings are now handled by HttpClient configuration
            //System.Net.ServicePointManager.MaxServicePointIdleTime = 20000000;
            // Lazy: only create the named mutex when CheckSingleInstance runs (avoids test host clashes).
            registerProtocolService = new RegisterProtocolService();
        }

        public bool CheckSingleInstance()
        {
            singleInstanceService ??= new SingleInstanceService("FAP");
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
            void ShutdownUi()
            {
                if (null != mainWindowModel)
                {
                    try
                    {
                        serviceProvider.GetService<IChatSession>()?.SavePeerHistory();
                    }
                    catch
                    {
                        // best-effort
                    }
                    mainWindowModel.Close();
                    trayIcon.Dispose();

                    model.GetShutdownLock();
                    singleInstanceService?.Dispose();
                    appLifetime.Shutdown(0);
                }
            }

            var ui = SafeObservableStatic.UiDispatcher;
            if (ui != null)
                ui.Invoke(ShutdownUi);
            else
                ShutdownUi();
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
            trayIcon.Search = new RelayCommand(Search);
            trayIcon.Chat = new RelayCommand(OpenChat);
            trayIcon.OpenExternal = new RelayCommand<object?>(OpenExternal);
            trayIcon.ShowIcon = true;
            shellNavigation = serviceProvider.GetRequiredService<IShellNavigation>();
            serviceProvider.GetRequiredService<IChatSession>().Load();
            if (showWindow)
                ShowMainWindow();
            if (!model.DisplayedHelp)
            {
                try { ShowQuickStart(); }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Quick Start failed; continuing without help dialog");
                }
            }
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

            // TODO: re-implement update checking against a new endpoint
            // updateChecker.Run();

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
                conversationController = serviceProvider.GetRequiredService<IConversationController>();
                watchdogController = serviceProvider.GetRequiredService<WatchdogController>();

                // Run as a standard client by default; allow watchdog to start an overlord via election
                model.IsDedicated = false;
                watchdogController.Start();
            }
            connectTiming.Mark(ConnectTimingPhases.LoadDone);
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
            var ui = serviceProvider.GetRequiredService<IGettingStartedUi>();
            ui.ShowGettingStarted();
            model.DisplayedHelp = true;
            model.Save();
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
            
            client = serviceProvider.GetRequiredService<FAP.Domain.Services.IListenerServiceFactory>().Create(false);
            client.Start(model.LocalNode.Port);
            connectionController.Start();
            connectTiming.Mark(ConnectTimingPhases.ClientListen);
            watchdogController?.OnClientListening();
            
            logger.LogDebug("ApplicationCore.StartClient: Client started successfully");
        }

        public async Task StartOverlordServerAsync()
        {
            logger.LogDebug("ApplicationCore.StartOverlordServer: Starting dedicated overlord server");
            
            model.IsDedicated = true;
            overlordManagerService.Start();

            // Wait until :40 answers health instead of a fixed 1s sleep
            using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(300) })
            {
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        var resp = await http.GetAsync("http://127.0.0.1:40/Fap.api/health");
                        if (resp.IsSuccessStatusCode)
                            break;
                    }
                    catch
                    {
                        // not ready yet
                    }
                    await Task.Delay(50);
                }
            }
            
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
                // Peers list = remote clients only (not overlord, not self).
                f.Filter = s => s.NodeType != ClientType.Overlord
                    && !string.Equals(s.ID, model.LocalNode.ID, StringComparison.Ordinal);
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

        private void Search() => NavigateShell("search");

        private void OpenChat()
        {
            ShowMainWindow();
            EnsureShell().NavigateToChat();
        }

        private void showUserInfo(object? obj)
        {
            var n = obj as Node;
            if (null != n)
            {
                var o = serviceProvider.GetRequiredService<UserInfoViewModel>();
                o.Node = n;
            }
        }

        private void Chat(object? o)
        {
            var peer = o as Node;
            if (peer == null)
                return;
            ShowMainWindow();
            serviceProvider.GetRequiredService<IChatSession>().OpenPeer(peer);
            EnsureShell().NavigateToChat(peer.ID);
        }

        private void Compare() => NavigateShell("compare");

        private void NavigateShell(string tag)
        {
            ShowMainWindow();
            EnsureShell().NavigateTag(tag);
        }

        private IShellNavigation EnsureShell()
        {
            shellNavigation ??= serviceProvider.GetRequiredService<IShellNavigation>();
            return shellNavigation;
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

        private void ViewQueue() => NavigateShell("queue");

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
                    {
                        // Local echo so the chat list updates even if overlord fan-out is delayed.
                        model.Messages.AddRotate(model.Nickname + ":" + mainWindowModel.CurrentChatMessage, 50);
                        SafeObservingCollectionManager.UpdateNowAsync();
                        connectionController.SendMessage(mainWindowModel.CurrentChatMessage);
                    }
                    break;
            }
            mainWindowModel.CurrentChatMessage = string.Empty;
        }

        private void viewShare(object? o)
        {
            logger.LogDebug("viewShare: Called with object={Type}", o?.GetType().Name ?? "null");

            var rc = o as Node;
            logger.LogDebug("viewShare: Cast to Node result={Nickname}", rc?.Nickname ?? "null");

            if (null == rc)
            {
                logger.LogWarning("viewShare called with null node");
                return;
            }

            try
            {
                ShowMainWindow();
                serviceProvider.GetRequiredService<IBrowseSessionHost>().OpenPeer(rc);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error opening browse session for node: {Nickname}", rc.Nickname);
            }
        }

        private void EditShares() => NavigateShell("shares");

        private void Settings() => NavigateShell("settings");

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
                    SafeObservableStatic.UiDispatcher?.Invoke(
                                                 () =>
                                                     {
                                                         if (null != mainWindowModel)
                                                         {
                                                             // Update status — one ConnectionState drives shell + home + InfoBar
                                                             {
                                                                 var state = model.Network.State;
                                                                 var nick = model.Nickname ?? string.Empty;
                                                                 window.IsMeshConnected = state == ConnectionState.Connected;
                                                                 window.ShellStatus = string.IsNullOrWhiteSpace(nick)
                                                                     ? state.ToString()
                                                                     : $"{state} · {nick}";

                                                                 var primary = new StringBuilder();
                                                                 primary.Append("Status: ");
                                                                 primary.Append(state);
                                                                 if (!string.IsNullOrWhiteSpace(nick))
                                                                 {
                                                                     primary.Append(" as ");
                                                                     primary.Append(nick);
                                                                 }
                                                                 window.NodeStatus = primary.ToString();

                                                                 var detail = new StringBuilder();
                                                                 if (state == ConnectionState.Connected)
                                                                 {
                                                                     if (model.Network.Overlord.Host ==
                                                                         model.LocalNode.Host)
                                                                     {
                                                                         detail.Append(overlordManagerService.IsOverlordActive
                                                                             ? "Hosting the mesh on this machine."
                                                                             : "Connected on yourself.");
                                                                     }
                                                                     else
                                                                     {
                                                                         detail.Append("Mesh host: ");
                                                                         Node? search =
                                                                             model.Network.Nodes.ToList().Where(
                                                                                 n =>
                                                                                 n.Host == model.Network.Overlord.Host &&
                                                                                 n.NodeType == ClientType.Client).
                                                                                 FirstOrDefault();
                                                                         if (null == search)
                                                                             detail.Append(model.Network.Overlord.Host);
                                                                         else
                                                                             detail.Append(search.Nickname);
                                                                     }
                                                                 }
                                                                 else if (overlordManagerService.IsOverlordActive)
                                                                 {
                                                                     detail.Append("Starting as mesh host…");
                                                                 }

                                                                 window.NodeStatusDetail = detail.ToString();
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
                                                     });
                }
                window = null!;
                try { await System.Threading.Tasks.Task.Delay(333, token); } catch { }
            }
        }

        #endregion
    }
}