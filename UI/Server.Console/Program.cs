using System;
using System.Threading.Tasks;
using FAP.Application;
using FAP.Application.DependencyInjection;
using FAP.Application.Services;
using FAP.Application.Views;
using FAP.Domain.Entities;
using Fap.Foundation.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Server.Console
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Program p = new Program();
            await p.RunAsync(args);
        }

        private IServiceProvider serviceProvider = null!;
        private ILogger<Program> logger = null!;
        private Model model = null!;

        private async Task RunAsync(string[] args)
        {
            if (Compose(args))
            {
                model = serviceProvider.GetRequiredService<Model>();
                model.Messages.CollectionChanged += Messages_CollectionChanged;

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

        private void Messages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
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
                builder.Services.AddOptions<FAP.Network.Server.FapWebOptions>()
                    .Bind(builder.Configuration.GetSection("Fap:Web"))
                    .Validate(o => o != null, "Fap:Web must be configured")
                    .ValidateOnStart();
                builder.Services.AddOptions<FAP.Network.Server.FapListenOptions>()
                    .Bind(builder.Configuration.GetSection("Fap:Web:Listen"))
                    .Validate(o => !string.IsNullOrWhiteSpace(o.Address) && o.Port > 0, "Listen Address and Port must be valid")
                    .ValidateOnStart();

                builder.Logging.ClearProviders();
                builder.Logging.AddConsole();
                builder.Logging.AddDebug();
                builder.Logging.AddEventLog();
                builder.Logging.SetMinimumLevel(LogLevel.Debug);

                var services = builder.Services;
                services.AddSingleton<IAppLifetime, NoOpAppLifetime>();
                services.AddFapCore(builder.Configuration);
                services.AddFapClient();

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
