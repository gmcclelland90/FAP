using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using FAP.Domain.Verbs;
using Fap.Foundation;
using FAP.Network.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FAP.Domain.Net
{
    public class LANPeerFinderService
    {
        private readonly BackgroundSafeObservable<DetectedNode> announcedAddresses =
            new BackgroundSafeObservable<DetectedNode>();

        private readonly IServiceProvider serviceProvider;
        private MulticastClientService mclient;

        public LANPeerFinderService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
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
            // Implementation for handling multicast receive
        }
    }
}