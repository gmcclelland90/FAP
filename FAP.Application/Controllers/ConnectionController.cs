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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FAP.Application.ViewModels;
using FAP.Domain.Entities;
using FAP.Domain.Services;
using System.Net.Http;
using FAP.Domain.Net;
using FAP.Shared.Interfaces;
using FAP.Network;
using FAP.Network.Services;
using FAP.Network.Server;
using Fap.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FAP.Domain; // For ConnectionState and ClientType
using FAP.Domain.Verbs.Multicast; // For WhoVerb
using FAP.Domain.Verbs; // For ChatVerb, ConnectVerb, UpdateVerb
using FAP.Network.Entities; // For NetworkRequest
using FAP.Domain.Services; // For IDService
using Fap.Foundation.Services; // For IDService
using FAP.Shared.ConnectTiming;

namespace FAP.Application.Controllers
{
    /// <summary>
    /// Handles automatically connecting the client to a server on the lan
    /// </summary>
    public class ConnectionController
    {
        private static readonly object sync = new object();
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<ConnectionController> logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ModernHttpClient> _httpLogger;
        private readonly Model model;
        private readonly MulticastServerService mserver;
        private readonly LANPeerFinderService peerFinder;
        private readonly IConnectTimingProbe connectTiming;
        private readonly Node transmitted = new Node();
        private readonly AutoResetEvent workerEvent = new AutoResetEvent(true);
        private bool run = true;
        private readonly List<LanPeer> attemptedPeers = new List<LanPeer>();

        public ConnectionController(IServiceProvider serviceProvider, Model m, ILogger<ConnectionController> logger)
        {
            model = m;
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            _httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            _httpLogger = serviceProvider.GetRequiredService<ILogger<ModernHttpClient>>();
            mserver = serviceProvider.GetRequiredService<MulticastServerService>();
            peerFinder = serviceProvider.GetRequiredService<LANPeerFinderService>();
            connectTiming = serviceProvider.GetRequiredService<IConnectTimingProbe>();
            setupLocalNetwork();
        }

        private void setupLocalNetwork()
        {
            // Domain.Entities.Network network = new Domain.Entities.Network();
            model.Network.NetworkName = "Local";
            model.Network.NetworkID = "Local";
            model.Network.State = ConnectionState.Disconnected;
            model.PropertyChanged += model_PropertyChanged;
            model.LocalNode.PropertyChanged += LocalNode_PropertyChanged;
            // model.Networks.Add(network);
        }

        private void LocalNode_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _ = Task.Run(() => CheckModelChangesAsync(null));
        }

        private void model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "State")
                workerEvent.Set();
        }

        public void SendMessage(string message)
        {
            var verb = new ChatVerb();
            verb.Message = message;
            verb.Nickname = model.LocalNode.Nickname;
            verb.SourceID = model.LocalNode.ID;
            _ = Task.Run(() => SendMessageAsync(verb));
        }

        private async void SendMessageAsync(object? o)
        {
            try
            {
                if (model.Network.State == ConnectionState.Connected)
                {
                    var client = new ModernHttpClient((INode)model.LocalNode, _httpLogger, _httpClientFactory.CreateClient("FapDefault"));
                    if (!await client.ExecuteAsync((IVerb)o!, model.Network.Overlord))
                    {
                        // Do not drop the whole session on a single chat send failure.
                        logger.LogWarning("Chat message was not accepted by overlord; staying connected");
                    }
                }
                else
                {
                    logger.LogWarning("Could not send message as you are not connected");
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to send chat message");
            }
        }

        public void Start()
        {
            peerFinder.Start();
            _ = Task.Run(() => ProcessLanConnectionAsync(CancellationToken.None));
        }


        public void Exit()
        {
            run = false;
            workerEvent.Set();
            Disconnect();
        }

        public async Task DisconnectAsync()
        {
            //Notify log off
            if (model.Network.State == ConnectionState.Connected)
            {
                var c = new ModernHttpClient(model.LocalNode, _httpLogger, _httpClientFactory.CreateClient("FapDefault"));
                var verb = new UpdateVerb();
                verb.Nodes.Add(new Node {ID = model.LocalNode.ID, Online = false});
                await c.ExecuteAsync(verb, model.Network.Overlord, 3000);

                //Remove peer so we dont reconnect straight away most likely
                DetectedNode? peer =
                    peerFinder.Peers.Where(p => p.Address == model.Network.Overlord.Location).FirstOrDefault();
                if (null != peer)
                    peerFinder.RemovePeer(peer);
                model.Network.State = ConnectionState.Disconnected;
            }
        }

        public void Disconnect()
        {
            _ = DisconnectAsync();
        }

        private async Task ProcessLanConnectionAsync(CancellationToken token)
        {
            logger.LogInformation("ProcessLanConnection started");
            mserver.SendMessage(WhoVerb.CreateRequest());
            connectTiming.Mark(ConnectTimingPhases.WhoSent);
            Domain.Entities.Network network = model.Network;
            network.PropertyChanged += network_PropertyChanged;
            while (run && !token.IsCancellationRequested)
            {
                if (network.State != ConnectionState.Connected)
                {
                    logger.LogDebug("Not connected, attempting to connect...");
                    //Not connected so connect automatically..

                    //Regenerate local secret to stop any updates if we reconnecting..
                    network.Overlord = new Node();
                    network.Overlord.Secret = IDService.CreateID();
                    //Clear old peers
                    network.Nodes.Clear();

                    //Build up a prioritised server list
                    var availibleNodes = new List<DetectedNode>();

                    List<DetectedNode> detectedPeers = peerFinder.Peers.ToList();
                    logger.LogDebug("Found {Count} detected peers", detectedPeers.Count);

                    //Prioritise a server we havent connected to already
                    foreach (DetectedNode peer in detectedPeers)
                    {
                        if (attemptedPeers.Where(s => s.Node == peer).Count() == 0)
                            availibleNodes.Add(peer);
                    }
                    foreach (LanPeer peer in attemptedPeers.OrderByDescending(x => x.LastConnectionTime))
                    {
                        availibleNodes.Add(peer.Node);
                    }

                    // If running as dedicated overlord and no peers found, connect to local overlord
                    logger.LogDebug("model.IsDedicated = {IsDedicated}", model.IsDedicated);
                    // If we have no detected peers, do NOT force connect to local port unless an overlord is already active
                    if (availibleNodes.Count == 0 && model.IsDedicated)
                    {
                        logger.LogInformation("No peers found, connecting to local overlord as dedicated server");
                        
                        // Try to find the actual overlord port by checking multicast announcements
                        var localOverlordNode = new DetectedNode();
                        var localOverlord = detectedPeers.FirstOrDefault(p => p.Address.Contains(model.LocalNode.Host));
                        
                        if (localOverlord != null)
                        {
                            localOverlordNode.Address = localOverlord.Address;
                            logger.LogInformation("Found local overlord via multicast: {Address}", localOverlordNode.Address);
                        }
                        else
                        {
                            // Fallback to port 40 if no multicast announcement found
                            localOverlordNode.Address = model.LocalNode.Host + ":40";
                            logger.LogInformation("No multicast announcement found, using fallback port: {Address}", localOverlordNode.Address);
                        }
                        
                        availibleNodes.Add(localOverlordNode);
                        logger.LogInformation("Added local overlord node: {Address}", localOverlordNode.Address);
                    }
                    else if (availibleNodes.Count == 0)
                    {
                        logger.LogDebug("No peers found; waiting for election or watchdog to start an overlord");
                    }

                    logger.LogDebug("Available nodes to connect to: {Count}", availibleNodes.Count);
                    while (network.State != ConnectionState.Connected && availibleNodes.Count > 0)
                    {
                        DetectedNode node = availibleNodes[0];
                        availibleNodes.RemoveAt(0);
                        logger.LogInformation("Attempting to connect to: {Address}", node.Address);
                        if (!await Connect(network, node))
                            peerFinder.RemovePeer(node);
                    }
                }
                if (network.State == ConnectionState.Connected)
                {
                    await CheckModelChanges();
                    //Check for network timeout

                    if ((Environment.TickCount - model.Network.Overlord.LastUpdate) > Model.UPLINK_TIMEOUT)
                    {
                        //We havent recently sent/recieved so went a noop so check we are still connected.
                        var noopVerb = new NoopVerb();
                        noopVerb.SourceID = model.LocalNode.ID;
                        noopVerb.AuthKey = model.Network.Overlord.Secret;
                        var client = new ModernHttpClient((INode)model.LocalNode, _httpLogger, _httpClientFactory.CreateClient("FapDefault"));
                        if (!await client.ExecuteAsync(noopVerb, model.Network.Overlord, 4000))
                        {
                            if (network.State == ConnectionState.Connected)
                            {
                                Disconnect();
                            }
                        }
                    }

                    await Task.Delay(10000, token);
                }
                else
                {
                    await Task.Delay(100, token);
                }
            }
        }


        private void CheckModelChangesAsync(object? o)
        {
            CheckModelChanges();
        }

        /// <summary>
        /// Whilst connected to a network 
        /// </summary>
        public async Task CheckModelChanges()
        {
            if (model.Network.State == ConnectionState.Connected)
            {
                UpdateVerb? verb = null;
                lock (sync)
                {
                    var data = new Dictionary<string, string>();
                    foreach (var entry in model.LocalNode.Data)
                    {
                        if (transmitted.IsKeySet(entry.Key))
                        {
                            if (transmitted.GetData(entry.Key) != entry.Value)
                            {
                                data.Add(entry.Key, entry.Value);
                            }
                        }
                        else
                        {
                            data.Add(entry.Key, entry.Value);
                        }
                    }
                    //Data has changed, transmit the changes.
                    if (data.Count > 0)
                    {
                        verb = new UpdateVerb();
                        var n = new Node();
                        n.ID = model.LocalNode.ID;
                        foreach (var change in data)
                        {
                            n.SetData(change.Key, change.Value);
                            transmitted.SetData(change.Key, change.Value);
                        }
                        verb.Nodes.Add(n);
                        
                        // Also update the local node in the network nodes list
                        var localNode = model.Network.Nodes.FirstOrDefault(node => node.ID == model.LocalNode.ID);
                        if (localNode != null)
                        {
                            foreach (var change in data)
                            {
                                localNode.SetData(change.Key, change.Value);
                            }
                        }
                    }
                }
                if (null != verb)
                {
                    var c = new ModernHttpClient((INode)model.LocalNode, _httpLogger, _httpClientFactory.CreateClient("FapDefault"));
                    if (!await c.ExecuteAsync(verb, model.Network.Overlord))
                        model.Network.State = ConnectionState.Disconnected;
                }
            }
        }

        private void network_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            //When the network state changes then reconnect if needed.
            if (e.PropertyName == "State")
                workerEvent.Set();
        }

        private async Task<bool> Connect(Domain.Entities.Network net, DetectedNode n)
        {
            try
            {
                logger.LogInformation("Client connecting to {Address}", n.Address);
                net.State = ConnectionState.Connecting;
                connectTiming.Mark(ConnectTimingPhases.ConnectStart);

                var verb = new ConnectVerb();
                verb.ClientType = ClientType.Client;
                verb.Address = model.LocalNode.Location; // This should be the client's address (port 30)
                verb.Secret = IDService.CreateID();
                var client = new ModernHttpClient((INode)model.LocalNode, _httpLogger, _httpClientFactory.CreateClient("FapDefault"));

                transmitted.Data.Clear();
                foreach (var info in model.LocalNode.Data.ToList())
                    transmitted.SetData(info.Key, info.Value);

                net.Overlord = new Node();
                net.Overlord.Location = n.Address;
                net.Overlord.Secret = verb.Secret;
                logger.LogDebug("Client using secret {Secret}", verb.Secret);
                logger.LogDebug("Attempting to execute ConnectVerb to {Address}", n.Address);
                var result = await client.ExecuteAsync(verb, n.Address);
                logger.LogDebug("ConnectVerb result: {Result}", result);
                if (result)
                {
                    net.State = ConnectionState.Connected;
                    net.Overlord.ID = verb.OverlordID;
                    
                    // Update the overlord secret with the one returned by the server
                    if (!string.IsNullOrEmpty(verb.Secret))
                    {
                        net.Overlord.Secret = verb.Secret;
                        logger.LogDebug("Updated overlord secret to: {Secret}", verb.Secret);
                    }
                    
                    connectTiming.Mark(ConnectTimingPhases.Connected);
                    logger.LogInformation("Client connected successfully");
                    return true;
                }
                else
                {
                    logger.LogWarning("ConnectVerb failed");
                    net.Overlord = new Node();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception during connection attempt");
                net.State = ConnectionState.Disconnected;
            }
            return false;
        }
    }
}