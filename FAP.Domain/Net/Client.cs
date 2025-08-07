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
using System.IO;
using System.Net;
using System.Text;
using FAP.Domain.Entities;
using FAP.Domain.Net;
using FAP.Domain.Verbs;
using FAP.Network;
using FAP.Shared.Entities;
using FAP.Shared.Interfaces;

namespace FAP.Domain.Net
{
    public class Client
    {
        private readonly int DEFAULT_TIMEOUT = 30000; //30 seconds
        private readonly Node callingNode;

        public Client(Node _callingNode)
        {
            callingNode = _callingNode;
        }

        public bool Execute(FAP.Shared.Interfaces.IVerb verb, Node destinationNode)
        {
            return Execute(verb, destinationNode, DEFAULT_TIMEOUT);
        }

        public bool Execute(FAP.Shared.Interfaces.IVerb verb, string destination)
        {
            return Execute(verb, destination, string.Empty, DEFAULT_TIMEOUT);
        }

        public bool Execute(FAP.Shared.Interfaces.IVerb verb, string destination, int timeout)
        {
            return Execute(verb, destination, string.Empty, timeout);
        }

        public bool Execute(NetworkRequest req, Node destination)
        {
            return Execute(req, destination, 30000);
        }

        public bool Execute(NetworkRequest req, Node destination, int timeout)
        {
            destination.LastUpdate = Environment.TickCount;
            var output = new NetworkRequest();
            if (!string.IsNullOrEmpty(destination.Secret) && string.IsNullOrEmpty(req.AuthKey))
                req.AuthKey = destination.Secret;
            return DoRequest(destination.Location, req, out output, timeout);
        }

        public bool Execute(FAP.Shared.Interfaces.IVerb verb, string destination, string authKey, int timeout)
        {
            return Execute(verb, new Node {Location = destination, Secret = authKey}, timeout);
        }

        public bool Execute(FAP.Shared.Interfaces.IVerb verb, Node destination, int timeout)
        {
            try
            {
                NetworkRequest request = verb.CreateRequest();
                var output = new NetworkRequest();
                destination.LastUpdate = Environment.TickCount;

                if (!string.IsNullOrEmpty(destination.Secret) && string.IsNullOrEmpty(request.AuthKey))
                    request.AuthKey = destination.Secret;

                if (!DoRequest(destination.Location, request, out output, timeout))
                    return false;

                if (!verb.ReceiveResponse(output))
                    return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DoRequest(string url, NetworkRequest input, out NetworkRequest result, int timeout)
        {
            result = new NetworkRequest();

            if (callingNode != null)
                input.SourceID = callingNode.ID;

            try
            {
                // Use ModernHttpClient internally to avoid deprecated WebRequest
                using var modernClient = new ModernHttpClient(callingNode);
                return modernClient.DoRequestAsync(url, input, result, timeout).GetAwaiter().GetResult();
            }
            catch
            {
                return false;
            }
        }
    }
}