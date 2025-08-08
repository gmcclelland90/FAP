using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using FAP.Domain.Verbs;
using FAP.Domain.Verbs.Multicast;
using Fap.Foundation;
using FAP.Network.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FAP.Domain.Net
{
    public class LANPeerFinderService
    {
        private readonly BackgroundSafeObservable<DetectedNode> announcedAddresses =
            new BackgroundSafeObservable<DetectedNode>();

        private readonly IServiceProvider serviceProvider;
        private MulticastClientService mclient = null!;
        private readonly Microsoft.Extensions.Logging.ILogger<LANPeerFinderService> logger;

        public LANPeerFinderService(IServiceProvider serviceProvider, Microsoft.Extensions.Logging.ILogger<LANPeerFinderService> logger)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            announcedAddresses.CollectionChanged += announcedAddresses_CollectionChanged;
        }

        public List<DetectedNode> Peers
        {
            get { return announcedAddresses.ToList(); }
        }

        private void announcedAddresses_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add)
            {
            }
        }

        public void RemovePeer(DetectedNode d)
        {
            announcedAddresses.Lock();
            if (announcedAddresses.Contains(d))
                announcedAddresses.Remove(d);
            announcedAddresses.Unlock();
        }

        public void Start()
        {
            lock (announcedAddresses)
            {
                if (null == mclient)
                {
                    mclient = serviceProvider.GetRequiredService<MulticastClientService>();
                    mclient.OnMultiCastRX += mclient_OnMultiCastRX;
                    mclient.StartListener();
                }
            }
        }

        private void mclient_OnMultiCastRX(string cmd)
        {
            try
            {
                logger.LogDebug("Received multicast message: {Cmd}", cmd);
                
                if (cmd.StartsWith(HelloVerb.Preamble))
                {
                    var helloVerb = new HelloVerb();
                    var detectedNode = helloVerb.ParseRequest(cmd);
                    
                    if (detectedNode != null)
                    {
                        logger.LogDebug("Parsed HelloVerb from {Address}", detectedNode.Address);
                        
                        announcedAddresses.Lock();
                        
                        // Check if we already have this node
                        var existingNode = announcedAddresses.FirstOrDefault(n => n.Address == detectedNode.Address);
                        if (existingNode != null)
                        {
                            // Update existing node
                            existingNode.NetworkName = detectedNode.NetworkName;
                            existingNode.OverlordID = detectedNode.OverlordID;
                            existingNode.NetworkID = detectedNode.NetworkID;
                            existingNode.Priority = detectedNode.Priority;
                            existingNode.CurrentUsers = detectedNode.CurrentUsers;
                            existingNode.MaxUsers = detectedNode.MaxUsers;
                            logger.LogDebug("Updated existing node: {Address}", detectedNode.Address);
                        }
                        else
                        {
                            // Add new node
                            announcedAddresses.Add(detectedNode);
                            logger.LogDebug("Added new node: {Address}", detectedNode.Address);
                        }
                        
                        announcedAddresses.Unlock();
                    }
                    else
                    {
                        logger.LogWarning("Failed to parse HelloVerb message: {Cmd}", cmd);
                    }
                }
                else if (cmd.StartsWith(WhoVerb.Message))
                {
                    logger.LogDebug("Received WhoVerb message");
                    // WhoVerb is handled by the server to trigger announcements
                }
                else
                {
                    logger.LogDebug("Received unknown multicast message: {Cmd}", cmd);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing multicast message");
            }
        }
    }
}