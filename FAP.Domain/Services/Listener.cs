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
using System.Net;
using System.Net.Http;
using FAP.Domain.Entities;
using FAP.Domain.Handlers;
using FAP.Domain.Net;
using FAP.Domain.Verbs;
using FAP.Network.Server;
using FAP.Network.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FAP.Domain.Services
{
    public class ListenerService
    {
        private readonly IServiceProvider serviceProvider;

        private readonly ModernHTTPHandler http;
        private readonly ILogger<ListenerService> logger;

        private readonly bool isServer;
        private readonly Model model;
        private IFAPHandler fap = null!;
        private ModernNodeServer listener = null!;

        public ListenerService(IServiceProvider serviceProvider, bool _isServer, ILogger<ListenerService> logger)
        {
            this.serviceProvider = serviceProvider;
            http = serviceProvider.GetRequiredService<ModernHTTPHandler>();
            isServer = _isServer;
            model = serviceProvider.GetRequiredService<Model>();
            this.logger = logger;
        }

        public bool IsRunning
        {
            get { return listener != null; }
        }

        public void Start(int inport)
        {
            listener = new ModernNodeServer(serviceProvider, serviceProvider.GetRequiredService<ILogger<ModernNodeServer>>());
            listener.OnRequestAsync += listener_OnRequestAsync;

            // Determine initial bind address and port once, then retry by incrementing port when needed
            var listenOptions = serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<FAP.Network.Server.FapListenOptions>>()?.Value;
            var listenAddress = listenOptions?.Address;
            bool addressIsAny = !string.IsNullOrWhiteSpace(listenAddress) &&
                                 (string.Equals(listenAddress, "0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(listenAddress, "::", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(listenAddress, "::0", StringComparison.OrdinalIgnoreCase));
            var ip = (!string.IsNullOrWhiteSpace(listenAddress) && !addressIsAny)
                ? IPAddress.Parse(listenAddress!)
                : IPAddress.Parse(model.LocalNode.Host);
            int port = (!isServer && listenOptions?.Port != null) ? listenOptions!.Port!.Value : inport;

            bool trybind = true;
            do
            {
                try
                {
                    logger.LogInformation("Attempting to bind HTTP listener on {Address}:{Port} (isServer={IsServer})", ip, port, isServer);
                    listener.Start(ip, port);
                    trybind = false;
                    if (isServer)
                    {
                        // Compute advertised address: avoid 0.0.0.0/:: when announcing or connecting
                        var advertiseIp = (IPAddress.Any.Equals(ip) || IPAddress.IPv6Any.Equals(ip))
                            ? IPAddress.Loopback
                            : ip;
                        // Use the advertised IP for server handler so peers see a connectable address
                        var f = new FAPServerHandler(advertiseIp,
                                                     port,
                                                     model,
                                                     serviceProvider.GetRequiredService<MulticastClientService>(),
                                                     serviceProvider.GetRequiredService<LANPeerFinderService>(),
                                                     serviceProvider.GetRequiredService<MulticastServerService>(),
                                                     serviceProvider.GetRequiredService<ILogger<FAPServerHandler>>(),
                                                     serviceProvider.GetRequiredService<IHttpClientFactory>(),
                                                     serviceProvider.GetRequiredService<ILogger<ModernHttpClient>>());
                        fap = f;
                        f.Start("Local", "Local");
                        // Do not overwrite the client's LocalNode with server bind info
                        // The client's LocalNode must continue to represent the client listener (port 30)
                    }
                    else
                    {
                        var f = new FAPClientHandler(model, serviceProvider.GetRequiredService<ShareInfoService>(),
                                                     serviceProvider.GetRequiredService<IConversationController>(),
                                                      serviceProvider.GetRequiredService<BufferService>(),
                                                      serviceProvider.GetRequiredService<ServerUploadLimiterService>(),
                                                      serviceProvider.GetRequiredService<ILogger<FAPClientHandler>>());
                        fap = f;
                        f.Start();
                        // Compute advertised address for the client as well
                        var advertiseIp = (IPAddress.Any.Equals(ip) || IPAddress.IPv6Any.Equals(ip))
                            ? IPAddress.Loopback
                            : ip;
                        try
                        {
                            model.LocalNode.Host = advertiseIp.ToString();
                            model.LocalNode.Port = port;
                        }
                        catch { }
                        model.ClientPort = port;
                    }
                }
                catch (Exception ex)
                {
                    // For overlords (isServer=true), don't retry - they should only use port 40
                    // For clients (isServer=false), retry with next port
                    if (isServer)
                    {
                        logger.LogError(ex, "Failed to bind overlord to port {Port}. Overlords must use port 40.", port);
                        throw new Exception($"Could not bind overlord to port {port}. Overlords must use port 40.");
                    }
                    else
                    {
                        logger.LogWarning(ex, "Failed to bind to port {Port}, trying next port", port);
                        // Try next port for client listener
                        port++;
                        if (inport + 100 < port)
                        {
                            throw new Exception("Could not bind listener");
                        }
                        logger.LogInformation("Retrying bind on {Address}:{Port}", ip, port);
                    }
                }
            } while (trybind);
        }

        public void Stop()
        {
            listener.Stop();
            listener.OnRequestAsync -= listener_OnRequestAsync;
            listener = null!;
            var server = fap as FAPServerHandler;
            if (null != server)
            {
                server.Stop();
            }
            else
            {
                var client = fap as FAPClientHandler;
                if (null != client)
                {
                }
            }
        }

        private async Task listener_OnRequestAsync(object sender, FAP.Network.Server.RequestEventArgs arg)
        {
            // Prefer path-based routing to avoid UA dependency
            bool isFapPath = arg.Request.Path.StartsWith("/Fap.app/");
            
            if (!isFapPath)
            {
                // HTTP request
                if (arg.Request.Method == "GET")
                {
                    // Await the async handler to ensure the response is written before returning
                    await http.HandleAsync(arg.Request.Path, arg);
                }
            }
            else
            {
                // FAP request
                await fap.HandleAsync(arg);
            }
        }

    }
}