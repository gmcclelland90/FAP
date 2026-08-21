using System;
using FAP.Application.Controllers;
using FAP.Application.Services;
using FAP.Application.ViewModel;
using FAP.Application.ViewModels;
using FAP.Domain.Entities;
using FAP.Domain.Handlers;
using FAP.Domain.Net;
using FAP.Domain.Services;
using FAP.Domain.Verbs;
using FAP.GuestWeb;
using FAP.Network.Services;
using FAP.Shared.ConnectTiming;
using Fap.Foundation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FAP.Application.DependencyInjection
{
    public static class FapServiceCollectionExtensions
    {
        /// <summary>
        /// Domain + network services shared by WPF, WinUI, and Server.Console hosts.
        /// </summary>
        public static IServiceCollection AddFapCore(this IServiceCollection services, IConfiguration? configuration = null)
        {
            if (configuration != null)
            {
                services.Configure<FAP.Network.Server.FapWebOptions>(configuration.GetSection("Fap:Web"));
                services.Configure<FAP.Network.Server.FapListenOptions>(configuration.GetSection("Fap:Web:Listen"));
                services.Configure<FapElectionOptions>(configuration.GetSection("Fap:Election"));
            }
            else
            {
                services.AddOptions<FapElectionOptions>();
            }

            services.AddHttpClient("FapDefault", c =>
            {
                c.Timeout = TimeSpan.FromSeconds(30);
                c.DefaultRequestHeaders.UserAgent.ParseAdd(Model.AppVersion);
            });

            services.AddSingleton<IConnectTimingProbe, ConnectTimingProbe>();
            services.AddSingleton<ShareInfoService>();
            services.AddSingleton<Model>();
            services.AddSingleton<ModernHTTPHandler>();
            services.AddSingleton<LANPeerFinderService>();
            services.AddSingleton<BufferService>();
            services.AddSingleton<ServerUploadLimiterService>();
            services.AddSingleton<OverlordManagerService>();
            services.AddSingleton<UpdateCheckerService>();
            services.AddSingleton<IListenerServiceFactory, ListenerServiceFactory>();

            services.AddMemoryCache();
            services.AddSingleton<WmiService>();
            services.AddSingleton<IHardwareInfoService, HardwareInfoService>();
            services.AddSingleton<ParallelWmiService>();

            services.AddSingleton<MulticastClientService>();
            services.AddSingleton<MulticastServerService>();

            services.AddSingleton<ICompareSpecProvider, CompareSpecProvider>();
            services.AddSingleton<IFapHttpClientFactory, FapHttpClientFactory>();
            services.AddFapGuestWeb();

            return services;
        }

        /// <summary>
        /// Application controllers and ViewModels (no presentation views).
        /// </summary>
        public static IServiceCollection AddFapClient(this IServiceCollection services)
        {
            services.AddSingleton<IPeerOrchestration, PeerOrchestration>();
            services.AddSingleton<ChatHistoryStore>();
            services.AddSingleton<ConversationController>();
            services.AddSingleton<IConversationController>(sp => sp.GetRequiredService<ConversationController>());
            services.AddSingleton<IChatSession>(sp => sp.GetRequiredService<ConversationController>());
            services.AddSingleton<ConnectionController>();
            services.AddSingleton<WatchdogController>();
            services.AddTransient<InterfaceController>();
            services.AddSingleton<ApplicationCore>();
            services.AddSingleton<DownloadQueueController>();
            // BrowserController is constructed per peer (needs Node); not registered in DI.
            services.AddSingleton<SharesController>();
            services.AddSingleton<SearchController>();
            services.AddSingleton<CompareController>();
            services.AddSingleton<SettingsController>();

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
            services.AddSingleton<ConversationViewModel>();
            services.AddSingleton<PopupWindowViewModel>();
            services.AddSingleton<InterfaceSelectionViewModel>();

            return services;
        }
    }
}
