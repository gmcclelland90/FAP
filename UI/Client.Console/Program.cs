using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using FAP.Domain.Entities;
using FAP.Foundation.Services;
using FAP.Network.Server;

namespace Client.Console
{
    class Program
    {
        static void Main(string[] a)
        {
            var builder = Host.CreateApplicationBuilder(a);
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();
            builder.Logging.AddEventLog(); // optional on Windows
            // Removed NLog bridge
            
            // Set minimum log level to Debug to see debug logs in Visual Studio
            builder.Logging.SetMinimumLevel(LogLevel.Debug);

            var services = builder.Services;
            services.AddSingleton<Model>();
            services.AddSingleton<FapLoggingProvider>(sp => new FapLoggingProvider(sp.GetRequiredService<Model>().Messages));

            using var host = builder.Build();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Client starting...");

            Program p = new Program();
            p.test();
        }


        private void test()
        {
            NodeServer server = new NodeServer();
            server.Start();

            for (int i = 0; i < 100; i++)
            {
                FAP.Network.Client.Client c = new FAP.Network.Client.Client();
                c.Test();
            }
            System.Console.ReadKey();
        }
    }
}
