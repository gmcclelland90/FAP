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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using FAP.Application;
using FAP.Application.ViewModels;
using FAP.Application.ViewModel;
using FAP.Application.Controllers;
using FAP.Application.Views;
using FAP.Domain;
using FAP.Domain.Entities;
using FAP.Domain.Net;
using FAP.Domain.Services;
using FAP.Domain.Verbs;
using FAP.Domain.Handlers;
using FAP.Network;
using FAP.Network.Services;
using Fap.Foundation;
using Fap.Presentation.Panels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;
using System.Waf.Presentation.Services;
using Fap.Presentation.Services;

namespace Fap.Presentation
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private IServiceProvider serviceProvider;
        private SplashScreen appSplash;

        private string GetImage()
        {
            System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
            System.Globalization.CultureInfo culture = Thread.CurrentThread.CurrentCulture;
            string resourceName = asm.GetName().Name + ".g";
            System.Resources.ResourceManager rm = new System.Resources.ResourceManager(resourceName, asm);
            System.Resources.ResourceSet resourceSet = rm.GetResourceSet(culture, true, true);
            List<string> resources = new List<string>();
            foreach (DictionaryEntry resource in resourceSet)
            {
                if(((string)resource.Key).StartsWith("images/splash%20screens/"))
                resources.Add((string)resource.Key);
            }
            rm.ReleaseAllResources();
            Random r = new Random();
            int i = r.Next(0, resources.Count());
            return resources[i];
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            if(e.Args.Contains("WAIT"))
                Thread.Sleep(5000);

            this.DispatcherUnhandledException += new System.Windows.Threading.DispatcherUnhandledExceptionEventHandler(App_DispatcherUnhandledException);
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

            base.OnStartup(e);
            
            // Initialize the SafeObservableStatic dispatcher for UI updates
            SafeObservableStatic.Dispatcher = this.Dispatcher;
            
            // Start the SafeObservingCollectionManager for UI collection synchronization
            SafeObservingCollectionManager.Start();
            
            if (Compose())
            {
                if (e.Args.Length == 1 && e.Args[0] == "WAIT")
                {
                    //Delay the application starting up, used when restarting.
                    Thread.Sleep(3000);
                }

                ApplicationCore core = serviceProvider.GetRequiredService<ApplicationCore>();

                if (!core.CheckSingleInstance())
                {
                    //An instance of fap is already running.

                    //If we got a download url then forward onto the runing instance of FAP
                    if (e.Args.Length == 2 && e.Args[0] == "-url")
                    {
                        Model model = new Model();
                        model.Load();

                        Client client = new Client(model.LocalNode);
                        AddDownload verb = new AddDownload();
                        verb.URL = e.Args[1];
                        if (client.Execute(verb, model.LocalNode))
                        {
                            //Download sent successfully
                            Shutdown(0);
                            return;
                        }
                        else
                        {
                            //Unsuccessful - Notify user
                            System.Windows.MessageBox.Show("Failed to add download via RPC!", "FAP", MessageBoxButton.OK, MessageBoxImage.Warning);
                            Shutdown(1);
                            return;
                        }
                    }
                    else
                    {
                        //Inform the user they cannot run multiple instances
                        System.Windows.MessageBox.Show("An instance of FAP is already running", "FAP", MessageBoxButton.OK, MessageBoxImage.Information);
                        Shutdown(1);
                        return;
                    }
                }

                string img = GetImage();
                appSplash = new SplashScreen(img);
                appSplash.Show(true);

                if (core.Load(false))
                {

                    // Run as dedicated overlord server instead of client
                    core.StartOverlordServer();
                    core.StartGUI(!(e.Args.Contains("STARTUP")));
                    //Was a url passed on startup?
                    if (e.Args.Length == 2 && e.Args[0] == "-url")
                    {
                        core.AddDownloadUrlWhenConnected(e.Args[1]);
                    }
                }
                else
                {
                    Shutdown(1);
                }
            }
            else
            {
                Shutdown(1);
            }
            if (null != appSplash)
                appSplash.Close(TimeSpan.FromSeconds(0));
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            System.ComponentModel.Win32Exception w32e = e.Exception as System.ComponentModel.Win32Exception;
            if ((w32e != null) && (w32e.NativeErrorCode == 0))
            {
                // ignore and continue: most likely cause is the splash screen loses focus during its showing period (for .NET 3.5 SP1)
                // https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=362735
                e.Handled = true;
            }
            else
            {
                Console.WriteLine(e.Exception.Message);
                if (null != serviceProvider)
                    LogManager.GetLogger("faplog").Fatal("Unhandled dispatcher exception", e.Exception);
                e.Handled = true;
            }
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            System.ComponentModel.Win32Exception w32e = e.Exception as System.ComponentModel.Win32Exception;
            if ((w32e != null) && (w32e.NativeErrorCode == 0))
            {
                // ignore and continue: most likely cause is the splash screen loses focus during its showing period (for .NET 3.5 SP1)
                // https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=362735
                e.Handled = true;
            }
            else
            {
                Console.WriteLine(e.Exception.Message);
                if (null != serviceProvider)
                    LogManager.GetLogger("faplog").Fatal("Unhandled exception", e.Exception);
                e.Handled = true;
            }
        }

         private bool Compose()
        {
             try
             {
                 var services = new ServiceCollection();
                 
                 // Register services from all modules
                 RegisterDomainServices(services);
                 RegisterNetworkServices(services);
                 RegisterApplicationServices(services);
                 RegisterGUIServices(services);

                 serviceProvider = services.BuildServiceProvider();
                 return true;
             }
             catch
             {
                 return false;
             }
        }

        private void RegisterDomainServices(IServiceCollection services)
        {
            services.AddSingleton<ShareInfoService>();
            services.AddSingleton<ListenerService>();
            services.AddSingleton<Model>();
            services.AddSingleton<ModernHTTPHandler>();
            services.AddSingleton<LANPeerFinderService>();
            services.AddSingleton<BufferService>();
            services.AddSingleton<ServerUploadLimiterService>();
            services.AddSingleton<LogService>();
            services.AddSingleton<OverlordManagerService>();
            services.AddSingleton<UpdateCheckerService>();
        }

        private void RegisterNetworkServices(IServiceCollection services)
        {
            services.AddSingleton<MulticastClientService>();
            services.AddSingleton<MulticastServerService>();
            services.AddSingleton<ModernHTTPHandler>();
        }

        private void RegisterApplicationServices(IServiceCollection services)
        {
            services.AddSingleton<IConversationController, ConversationController>();
            services.AddSingleton<ConnectionController>();
            services.AddSingleton<WatchdogController>();
            services.AddTransient<InterfaceController>();
            services.AddSingleton<ApplicationCore>();
            services.AddSingleton<DownloadQueueController>();
            services.AddSingleton<BrowserController>();
            services.AddSingleton<SharesController>();
        }

        private void RegisterGUIServices(IServiceCollection services)
        {
            // Register UI Controllers
            services.AddSingleton<IPopupWindowController, ModernPopupWindowController>();
            services.AddTransient<IPopupWindow, TabWindow>();
            services.AddSingleton<ModernSystemTrayService>();
            
            // Register Views
            services.AddTransient<MainWindow, MainWindow>();
            services.AddTransient<MessageBox, MessageBox>();
            services.AddTransient<IMessageBoxView, MessageBox>();
            services.AddTransient<Fap.Presentation.Panels.DownloadQueue, Fap.Presentation.Panels.DownloadQueue>();
            services.AddTransient<SettingsPanel, SettingsPanel>();
            services.AddTransient<TabWindow, TabWindow>();
            services.AddTransient<Query, Query>();
            services.AddTransient<BrowsePanel, BrowsePanel>();
            services.AddTransient<IBrowserView, BrowsePanel>();
            services.AddTransient<LogPanel, LogPanel>();
            services.AddTransient<SharesPanel, SharesPanel>();
            services.AddTransient<TrayIcon, TrayIcon>();
            services.AddTransient<ComparePanel, ComparePanel>();
            services.AddTransient<Fap.Presentation.Panels.Conversation, Fap.Presentation.Panels.Conversation>();
            services.AddTransient<UserInfoPanel, UserInfoPanel>();
            services.AddTransient<InterfaceSelection, InterfaceSelection>();
            services.AddTransient<MessageService, MessageService>();
            services.AddTransient<SearchPanel, SearchPanel>();
            services.AddTransient<WebPanel, WebPanel>();
            services.AddTransient<IInterfaceSelectionView, InterfaceSelection>();
            services.AddTransient<System.Waf.Applications.Services.IMessageService, System.Waf.Presentation.Services.MessageService>();
            services.AddTransient<ISharesView, SharesPanel>();
            services.AddTransient<IQuery, Query>();
            services.AddTransient<IWebPanel, WebPanel>();
            services.AddTransient<ITrayIconView, TrayIcon>();
            services.AddTransient<IMainWindow, MainWindow>();
            services.AddTransient<ISearchView, SearchPanel>();
            services.AddTransient<IDownloadQueue, Fap.Presentation.Panels.DownloadQueue>();
            services.AddTransient<ICompareView, ComparePanel>();
            services.AddTransient<ISettingsView, SettingsPanel>();

            // Register ViewModels
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<TrayIconViewModel>();
            services.AddSingleton<WebViewModel>();
            services.AddSingleton<UserInfoViewModel>();
            services.AddSingleton<DownloadQueueViewModel>();
            services.AddSingleton<BrowserViewModel>();
            services.AddSingleton<SharesViewModel>();
            services.AddSingleton<CompareViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<SearchViewModel>();
            services.AddSingleton<MessageBoxViewModel>();
            services.AddSingleton<QueryViewModel>();
            services.AddSingleton<SearchController>();
            services.AddSingleton<CompareController>();
            services.AddSingleton<DownloadQueueController>();
            services.AddSingleton<SettingsController>();
            services.AddSingleton<BrowserController>();
            services.AddSingleton<ShareInfoService>();
            services.AddSingleton<ConversationViewModel>();
            services.AddTransient<IConverstationView, Fap.Presentation.Panels.Conversation>();
            services.AddSingleton<PopupWindowViewModel>();
            services.AddSingleton<InterfaceSelectionViewModel>();
		}

        protected override void OnExit(ExitEventArgs e)
        {
            if (serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            base.OnExit(e);
        }
    }
}
