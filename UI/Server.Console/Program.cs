using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FAP.Domain;
using FAP.Domain.Services;
using FAP.Domain.Handlers;
using FAP.Application;
using FAP.Application.Controllers;
using FAP.Application.Views;
using FAP.Network;
using FAP.Network.Services;
using FAP.Domain.Verbs;
using System.Net;
using System.Waf.Applications.Services;
using FAP.Application.Views;
using NLog.Filters;
using NLog;
using Microsoft.Extensions.DependencyInjection;
using System.Waf.Presentation.Services; // For MessageService
using FAP.Domain.Entities; // For Model
using FAP.Domain.Net; // For LANPeerFinderService

namespace Server.Console
{
    class Program
    {
        static void Main(string[] args)
        {

            
            Program p = new Program();
            p.Run();
        }

        private IServiceProvider serviceProvider;
        private LogService logService;
        private Model model;

        private void Run()
        {

            if(Compose())
            {
                logService = serviceProvider.GetRequiredService<LogService>();
                logService.Filter = LogLevel.Trace;
                model = serviceProvider.GetRequiredService<Model>();
                model.Messages.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(Messages_CollectionChanged);

                ApplicationCore core = serviceProvider.GetRequiredService<ApplicationCore>();
                core.Load(true);
                core.StartOverlordServer();
               
                System.Console.WriteLine("Server started");
                System.Console.ReadKey();
            }
            else
            {
                System.Console.WriteLine("Program composition failed");
                System.Console.ReadKey();
            }
        }

        private void Messages_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems)
                    System.Console.WriteLine(item);
            }
        }

        private bool Compose()
        {
             try
             {
                 var services = new ServiceCollection();
                 
                 // Register domain services
                 services.AddSingleton<ShareInfoService>();
                 services.AddSingleton<ListenerService>();
                 services.AddSingleton<Model>();
                 services.AddSingleton<ModernHTTPHandler>();
                 services.AddSingleton<LANPeerFinderService>();
                 services.AddSingleton<BufferService>();
                 services.AddSingleton<ServerUploadLimiterService>();
                 services.AddSingleton<LogService>();
                 services.AddSingleton<OverlordManagerService>();

                 // Register network services
                 services.AddSingleton<MulticastClientService>();
                 services.AddSingleton<MulticastServerService>();

                 // Register application services
                 services.AddSingleton<IConversationController, ConversationController>();
                 services.AddSingleton<ConnectionController>();
                 services.AddSingleton<WatchdogController>();
                 services.AddTransient<InterfaceController>();
                 services.AddSingleton<ApplicationCore>();

                 // Register additional services for server
                 services.AddTransient<IMessageService, MessageService>();
                 services.AddTransient<IInterfaceSelectionView, InterfaceSelectionView>();
                 services.AddTransient<ISharesView, SharesView>();
                 services.AddTransient<IQuery, Query>();

                 serviceProvider = services.BuildServiceProvider();
                 return true;
             }
             catch
             {
                 return false;
             }
        }
    }
}
