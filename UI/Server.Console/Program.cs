using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Waf.Presentation.Services; // For MessageService
using FAP.Domain.Entities; // For Model
using FAP.Domain.Net; // For LANPeerFinderService

namespace Server.Console
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Program p = new Program();
            await p.RunAsync(args);
        }

        private IServiceProvider serviceProvider;
        private ILogger<Program> logger;
        private Model model;

        private async Task RunAsync(string[] args)
        {

            if(Compose(args))
            {
                model = serviceProvider.GetRequiredService<Model>();
                model.Messages.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(Messages_CollectionChanged);

                ApplicationCore core = serviceProvider.GetRequiredService<ApplicationCore>();
                core.Load(true);
                await core.StartOverlordServerAsync();
               
                logger?.LogInformation("Server started");
                System.Console.ReadKey();
            }
            else
            {
                logger?.LogError("Program composition failed");
                System.Console.ReadKey();
            }
        }

        private void Messages_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems)
                    logger?.LogInformation("{Message}", item?.ToString());
            }
        }

        private bool Compose(string[] args)
        {
            try
             {
                 var builder = Host.CreateApplicationBuilder(args);
                 // Bind FAP web options from configuration if present
                 builder.Services.AddOptions<FAP.Network.Server.FapWebOptions>()
                     .Bind(builder.Configuration.GetSection("Fap:Web"))
                     .Validate(o => o != null, "Fap:Web must be configured")
                     .ValidateOnStart();
                 builder.Services.AddOptions<FAP.Network.Server.FapListenOptions>()
                     .Bind(builder.Configuration.GetSection("Fap:Web:Listen"))
                     .Validate(o => !string.IsNullOrWhiteSpace(o.Address) && o.Port > 0, "Listen Address and Port must be valid")
                     .ValidateOnStart();

                 // Logging: MEL + optional NLog bridge during migration
                 builder.Logging.ClearProviders();
                 builder.Logging.AddConsole();
                 builder.Logging.AddDebug();
                 builder.Logging.AddEventLog(); // optional on Windows
                 // Removed NLog bridge
                 
                 // Set minimum log level to Debug to see debug logs in Visual Studio
                 builder.Logging.SetMinimumLevel(LogLevel.Debug);
                 
                 var services = builder.Services;

                 // Register domain services
                 services.AddSingleton<ShareInfoService>();
                 services.AddSingleton<ListenerService>();
                 services.AddSingleton<Model>();
                 services.AddSingleton<UpdateCheckerService>();
                 services.AddSingleton<ModernHTTPHandler>();
                 services.AddSingleton<LANPeerFinderService>();
                 services.AddSingleton<BufferService>();
                 services.AddSingleton<ServerUploadLimiterService>();
                 services.AddSingleton<OverlordManagerService>();

                  // WMI and hardware info services
                  services.AddMemoryCache();
                  services.AddSingleton<Fap.Foundation.WmiService>();
                  services.AddSingleton<Fap.Foundation.IHardwareInfoService, Fap.Foundation.HardwareInfoService>();
                  services.AddSingleton<Fap.Foundation.ParallelWmiService>();

                 // Register network services
                 services.AddSingleton<MulticastClientService>();
                 services.AddSingleton<MulticastServerService>();

                 // Register application services
                 services.AddSingleton<IConversationController, ConversationController>();
                 services.AddSingleton<ConnectionController>();
                 services.AddSingleton<WatchdogController>();
                 services.AddTransient<InterfaceController>();
                 services.AddTransient<FAP.Application.ViewModels.InterfaceSelectionViewModel>();
                 services.AddSingleton<ApplicationCore>();

                 // Register additional services for server
                 services.AddTransient<IMessageService, MessageService>();
                 services.AddTransient<IInterfaceSelectionView, InterfaceSelectionView>();
                 services.AddTransient<ISharesView, SharesView>();
                 services.AddTransient<IQuery, Query>();

                 serviceProvider = builder.Build().Services;
                 logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                 return true;
             }
             catch
             {
                 return false;
             }
        }
    }
}
