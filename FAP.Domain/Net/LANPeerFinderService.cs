using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using FAP.Domain.Verbs;
using FAP.Domain.Verbs.Multicast;
using Fap.Foundation;
using FAP.Network.Services;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace FAP.Domain.Net
{
    public class LANPeerFinderService
    {
        private readonly BackgroundSafeObservable<DetectedNode> announcedAddresses =
            new BackgroundSafeObservable<DetectedNode>();

        private readonly IServiceProvider serviceProvider;
        private MulticastClientService mclient;
        private readonly Logger logger;

        public LANPeerFinderService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            logger = LogManager.GetLogger("faplog");
            announcedAddresses.CollectionChanged += announcedAddresses_CollectionChanged;
        }

        public List<DetectedNode> Peers
        {
            get { return announcedAddresses.ToList(); }
        }

        private void announcedAddresses_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
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
                logger.Debug($"Received multicast message: {cmd}");
                
                if (cmd.StartsWith(HelloVerb.Preamble))
                {
                    var helloVerb = new HelloVerb();
                    var detectedNode = helloVerb.ParseRequest(cmd);
                    
                    if (detectedNode != null)
                    {
                        logger.Debug($"Parsed HelloVerb from {detectedNode.Address}");
                        
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
                            logger.Debug($"Updated existing node: {detectedNode.Address}");
                        }
                        else
                        {
                            // Add new node
                            announcedAddresses.Add(detectedNode);
                            logger.Debug($"Added new node: {detectedNode.Address}");
                        }
                        
                        announcedAddresses.Unlock();
                    }
                    else
                    {
                        logger.Warn($"Failed to parse HelloVerb message: {cmd}");
                    }
                }
                else if (cmd.StartsWith(WhoVerb.Message))
                {
                    logger.Debug("Received WhoVerb message");
                    // WhoVerb is handled by the server to trigger announcements
                }
                else
                {
                    logger.Debug($"Received unknown multicast message: {cmd}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error processing multicast message");
            }
        }
    }
}