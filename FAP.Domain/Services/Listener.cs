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
using FAP.Domain.Entities;
using FAP.Domain.Handlers;
using FAP.Domain.Net;
using FAP.Domain.Verbs;
using FAP.Network.Server;
using FAP.Network.Services;
using HttpServer;
using Microsoft.Extensions.DependencyInjection;

namespace FAP.Domain.Services
{
    public class ListenerService
    {
        private readonly IServiceProvider serviceProvider;

        private readonly HTTPHandler http;

        private readonly bool isServer;
        private readonly Model model;
        private IFAPHandler fap;
        private NodeServer listener;

        public ListenerService(IServiceProvider serviceProvider, bool _isServer)
        {
            this.serviceProvider = serviceProvider;
            http = serviceProvider.GetRequiredService<HTTPHandler>();
            isServer = _isServer;
            model = serviceProvider.GetRequiredService<Model>();
        }

        public bool IsRunning
        {
            get { return listener != null; }
        }

        public void Start(int inport)
        {
            listener = new NodeServer();
            listener.OnRequest += listener_OnRequest;

            bool trybind = true;
            int port = inport;
            do
            {
                try
                {
                    listener.Start(IPAddress.Parse(model.LocalNode.Host), port);
                    trybind = false;
                    if (isServer)
                    {
                        var f = new FAPServerHandler(IPAddress.Parse(model.LocalNode.Host),
                                                     port,
                                                     model,
                                                     serviceProvider.GetRequiredService<MulticastClientService>(),
                                                     serviceProvider.GetRequiredService<LANPeerFinderService>(),
                                                     serviceProvider.GetRequiredService<MulticastServerService>());
                        fap = f;
                        f.Start("Local", "Local");
                    }
                    else
                    {
                        var f = new FAPClientHandler(model, serviceProvider.GetRequiredService<ShareInfoService>(),
                                                     serviceProvider.GetRequiredService<IConversationController>(),
                                                     serviceProvider.GetRequiredService<BufferService>(),
                                                     serviceProvider.GetRequiredService<ServerUploadLimiterService>());
                        fap = f;
                        f.Start();
                        model.ClientPort = port;
                    }
                }
                catch
                {
                    //Try again
                    port++;
                    if (inport + 100 < port)
                    {
                        throw new Exception("Could to bind listener");
                    }
                }
            } while (trybind);
        }

        public void Stop()
        {
            listener.Stop();
            listener.OnRequest -= listener_OnRequest;
            listener = null;
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

        private bool listener_OnRequest(RequestType type, RequestEventArgs arg)
        {
            if (type == RequestType.HTTP)
            {
                if (arg.Request.Method == "GET")
                    return http.Handle(arg.Request.Uri.LocalPath, arg);
            }
            else
            {
                return fap.Handle(arg);
            }
            return false;
        }
    }
}