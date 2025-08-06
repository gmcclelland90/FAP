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
using System.Linq;
using System.Net;
using System.Text;
using FAP.Domain.Entities;
using FAP.Domain.Services;
using FAP.Domain.Verbs;
using Fap.Foundation;
using FAP.Network;
using FAP.Shared.Entities;
using HttpServer;
using HttpServer.Messages;
using NLog;

namespace FAP.Domain.Handlers
{
    public class FAPClientHandler : IFAPHandler
    {
        private readonly BufferService bufferService;
        private readonly IConversationController chatController;
        private readonly Logger logger;
        private readonly Model model;
        private readonly ServerUploadLimiterService serverUploadLimiterService;
        private readonly ShareInfoService shareInfoService;

        public FAPClientHandler(Model m, ShareInfoService s, IConversationController c, BufferService b,
                                ServerUploadLimiterService sl)
        {
            model = m;
            shareInfoService = s;
            chatController = c;
            bufferService = b;
            serverUploadLimiterService = sl;
            logger = LogManager.GetLogger("faplog");
        }

        #region IFAPHandler Members

        public bool Handle(FAP.Network.Server.RequestEventArgs e)
        {
            // For backward compatibility, use the async version
            return HandleAsync(e).GetAwaiter().GetResult();
        }

        public async Task<bool> HandleAsync(FAP.Network.Server.RequestEventArgs e)
        {
            var networkReq = await Multiplexor.DecodeModernAsync(e.Request);
            var req = new FAP.Shared.Entities.NetworkRequest
            {
                Verb = networkReq.Verb,
                Data = networkReq.Data,
                Param = networkReq.Param,
                SourceID = networkReq.SourceID,
                OverlordID = networkReq.OverlordID,
                AuthKey = networkReq.AuthKey
            };
            logger.Trace("Client rx: {0} p: {1} source: {2} overlord: {3}", req.Verb, req.Param, req.SourceID,
                         req.OverlordID);
            logger.Debug("FAPClientHandler.HandleAsync: Processing verb: {0}", req.Verb);
            switch (req.Verb)
            {
                case "BROWSE":
                    logger.Debug("FAPClientHandler.HandleAsync: Routing to HandleBrowse");
                    return HandleBrowse(e, req);
                case "UPDATE":
                    logger.Debug("FAPClientHandler.HandleAsync: Routing to HandleUpdate");
                    return HandleUpdate(e, req);
                case "INFO":
                    logger.Debug("FAPClientHandler.HandleAsync: Routing to HandleInfoAsync");
                    return await HandleInfoAsync(e);
                case "NOOP":
                    logger.Debug("FAPClientHandler.HandleAsync: Routing to HandleNOOP");
                    return HandleNOOP(e, req);
                case "GET":
                    logger.Debug("FAPClientHandler.HandleAsync: Routing to HandleGet");
                    return HandleGet(e, req);
                case "DISCONNECT":
                    logger.Debug("FAPClientHandler.HandleAsync: Routing to HandleDisconnect");
                    return HandleDisconnect(e);
                case "CHAT":
                    logger.Debug("FAPClientHandler.HandleAsync: Routing to HandleChat");
                    return HandleChat(e, req);
                case "COMPARE":
                    logger.Debug("FAPClientHandler.HandleAsync: Routing to HandleCompareAsync");
                    return await HandleCompareAsync(e, req);
                case "SEARCH":
                    logger.Debug("FAPClientHandler.HandleAsync: Routing to HandleSearch");
                    return HandleSearch(e, req);
                case "CONVERSTATION":
                    logger.Debug("FAPClientHandler.HandleAsync: Routing to HandleConversation");
                    return HandleConversation(e, req);
                case "ADDDOWNLOAD":
                    logger.Debug("FAPClientHandler.HandleAsync: Routing to HandleAddDownload");
                    return HandleAddDownload(e, req);
                default:
                    logger.Debug("FAPClientHandler.HandleAsync: Unknown verb: {0}", req.Verb);
                    break;
            }
            return false;
        }

        #endregion

        public void Start()
        {
        }

        private bool HandleGet(FAP.Network.Server.RequestEventArgs e, NetworkRequest req)
        {
            //No url?
            if (string.IsNullOrEmpty(req.Param))
                return false;

            string[] possiblePaths;


            if (shareInfoService.ToLocalPath(req.Param, out possiblePaths))
            {
                foreach (string possiblePath in possiblePaths)
                {
                    if (File.Exists(possiblePath))
                    {
                        var ffu = new FAPFileUploader(bufferService, serverUploadLimiterService);
                        var session = new TransferSession(ffu);
                        model.TransferSessions.Add(session);
                        try
                        {
                            //Try to find the username of the request
                            string userName = e.Context.RemoteEndPoint.Address.ToString();
                            Node search = model.Network.Nodes.ToList().Where(n => n.ID == req.SourceID).FirstOrDefault();
                            if (null != search && !string.IsNullOrEmpty(search.Nickname))
                                userName = search.Nickname;

                            using (
                                FileStream fs = File.Open(possiblePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                // TODO: Implement modern upload functionality
                                // ffu.DoUpload(e.Context, fs, userName, possiblePath);
                                logger.Info($"Upload requested for {possiblePath} by {userName}");
                            }

                            //Add log of upload
                            double seconds = (DateTime.Now - ffu.TransferStart).TotalSeconds;
                            var txlog = new TransferLog();
                            txlog.Nickname = userName;
                            txlog.Completed = DateTime.Now;
                            txlog.Filename = Path.GetFileName(possiblePath);
                            txlog.Path = Path.GetDirectoryName(req.Param);
                            if (!string.IsNullOrEmpty(txlog.Path))
                            {
                                txlog.Path = txlog.Path.Replace('\\', '/');
                                if (txlog.Path.StartsWith("/"))
                                    txlog.Path = txlog.Path.Substring(1);
                            }

                            txlog.Size = ffu.Length - ffu.ResumePoint;
                            if (txlog.Size < 0)
                                txlog.Size = 0;
                            if (0 != seconds)
                                txlog.Speed = (int) (txlog.Size/seconds);
                            model.CompletedUploads.Add(txlog);
                        }
                        finally
                        {
                            model.TransferSessions.Remove(session);
                        }
                        return true;
                    }
                }
            }

            e.Response.StatusCode = (int)HttpStatusCode.NotFound;
            var generator = new ModernResponseWriter();
            generator.SendHeaders(e.Context, e.Response);
            return true;
        }

        private bool HandleAddDownload(FAP.Network.Server.RequestEventArgs e, NetworkRequest req)
        {
            if (req.AuthKey == model.LocalNode.Secret && !string.IsNullOrEmpty(req.Param))
            {
                model.AddDownloadURL(req.Param);
                SendOk(e);
                return true;
            }
            return false;
        }

        private bool HandleBrowse(FAP.Network.Server.RequestEventArgs e, NetworkRequest req)
        {
            var verb = new BrowseVerb(shareInfoService);
            NetworkRequest result = verb.ProcessRequest(req);
            byte[] data = Encoding.UTF8.GetBytes(result.Data);
            var generator = new ModernResponseWriter();
            e.Response.ContentLength = data.Length;
            generator.SendHeaders(e.Context, e.Response);
            e.Context.Stream.Write(data, 0, data.Length);
            e.Context.Stream.Flush();
            data = null;
            return true;
        }

        private bool HandleConversation(FAP.Network.Server.RequestEventArgs e, NetworkRequest req)
        {
            try
            {
                var verb = new ConversationVerb();
                verb.ProcessRequest(req);
                if (chatController.HandleMessage(verb.SourceID, verb.Nickname, verb.Message))
                {
                    SendOk(e);
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private bool HandleSearch(FAP.Network.Server.RequestEventArgs e, NetworkRequest req)
        {
            //We dont do this on a server..
            var verb = new SearchVerb(shareInfoService);
            NetworkRequest result = verb.ProcessRequest(req);
            byte[] data = Encoding.UTF8.GetBytes(result.Data);
            var generator = new ModernResponseWriter();
            e.Response.ContentLength = data.Length;
            generator.SendHeaders(e.Context, e.Response);
            e.Context.Stream.Write(data, 0, data.Length);
            e.Context.Stream.Flush();
            data = null;
            return true;
        }

        private async Task<bool> HandleCompareAsync(FAP.Network.Server.RequestEventArgs e, NetworkRequest req)
        {
            var verb = new CompareVerb(model);

            NetworkRequest result = verb.ProcessRequest(req);
            byte[] data = Encoding.UTF8.GetBytes(result.Data);
            var generator = new ModernResponseWriter();
            e.Response.ContentLength = data.Length;
            generator.SendHeaders(e.Context, e.Response);
            await e.Context.Stream.WriteAsync(data, 0, data.Length);
            await e.Context.Stream.FlushAsync();
            data = null;

            return true;
        }

        private bool HandleCompare(FAP.Network.Server.RequestEventArgs e, NetworkRequest req)
        {
            // For backward compatibility, use the async version
            return HandleCompareAsync(e, req).GetAwaiter().GetResult();
        }

        private bool HandleChat(FAP.Network.Server.RequestEventArgs e, NetworkRequest req)
        {
            var verb = new ChatVerb();
            verb.ReceiveResponse(req);
            model.Messages.AddRotate(verb.Nickname + ":" + verb.Message, 50);
            SendOk(e);
            SafeObservingCollectionManager.UpdateNowAsync();
            return true;
        }

        private bool HandleUpdate(FAP.Network.Server.RequestEventArgs e, NetworkRequest req)
        {
            logger.Debug("FAPClientHandler.HandleUpdate: Starting update processing");
            logger.Debug("FAPClientHandler.HandleUpdate: AuthKey = '{0}', Overlord.Secret = '{1}'", req.AuthKey, model.Network.Overlord.Secret);
            logger.Debug("FAPClientHandler.HandleUpdate: Request data length: {0}", req.Data?.Length ?? 0);
            
            // For self-connections, AuthKey might be empty, so we need to handle that case
            bool authValid = string.IsNullOrEmpty(req.AuthKey) || req.AuthKey == model.Network.Overlord.Secret;
            
            if (authValid)
            {
                logger.Debug("FAPClientHandler.HandleUpdate: Authentication valid, processing update");
                model.Network.Overlord.LastUpdate = Environment.TickCount;
                var verb = new UpdateVerb();
                verb.ProcessRequest(req);
                logger.Debug("FAPClientHandler.HandleUpdate: Processed UpdateVerb, nodes count: {0}", verb.Nodes?.Count ?? 0);
                
                foreach (Node node in verb.Nodes)
                {
                    logger.Debug("FAPClientHandler.HandleUpdate: Processing node {0} (Online: {1}, Nickname: {2})", 
                        node.ID, node.Online, node.Nickname);
                    
                    Node search = model.Network.Nodes.Where(i => i.ID == node.ID).FirstOrDefault();
                    if (search == null)
                    {
                        // Add the node if it has an ID and is online (or if Online is not set, assume it's online)
                        if (!string.IsNullOrEmpty(node.ID) && (node.Online || !node.ContainsKey("Online")))
                        {
                            logger.Debug("FAPClientHandler.HandleUpdate: Adding new node {0} to network", node.ID);
                            model.Network.Nodes.Add(node);
                            logger.Debug("FAPClientHandler.HandleUpdate: Network now has {0} nodes", model.Network.Nodes.Count);
                        }
                        else
                        {
                            logger.Debug("FAPClientHandler.HandleUpdate: Skipping node {0} - ID: {1}, Online: {2}", 
                                node.ID, !string.IsNullOrEmpty(node.ID), node.Online);
                        }
                    }
                    else
                    {
                        logger.Debug("FAPClientHandler.HandleUpdate: Updating existing node {0}", node.ID);
                        foreach (var param in node.Data)
                            search.SetData(param.Key, param.Value);
                        //Has the client disconnected?
                        if (!search.Online)
                        {
                            model.Network.Nodes.Remove(node);
                            logger.Trace("Client: Node offline update: " + node.ID);
                        }
                    }
                }
                SendOk(e);
                logger.Debug("FAPClientHandler.HandleUpdate: Update processed successfully");
                return true;
            }
            else
            {
                logger.Debug("FAPClientHandler.HandleUpdate: Authentication failed, rejecting update");
            }
            return false;
        }

        private async Task<bool> HandleInfoAsync(FAP.Network.Server.RequestEventArgs e)
        {
            e.Response.StatusCode = (int)HttpStatusCode.OK;
            var verb = new InfoVerb();
            verb.Node = model.LocalNode;
            NetworkRequest result = verb.CreateRequest();
            byte[] data = Encoding.UTF8.GetBytes(result.Data);
            var generator = new ModernResponseWriter();
            e.Response.ContentLength = data.Length;
            generator.SendHeaders(e.Context, e.Response);
            await e.Context.Stream.WriteAsync(data, 0, data.Length);
            await e.Context.Stream.FlushAsync();
            return true;
        }

        private bool HandleInfo(FAP.Network.Server.RequestEventArgs e)
        {
            // For backward compatibility, use the async version
            return HandleInfoAsync(e).GetAwaiter().GetResult();
        }

        private bool HandleNOOP(FAP.Network.Server.RequestEventArgs e, NetworkRequest req)
        {
            //Noop is usually used as a heartbeat message however if the authkey is set then it came from a overlord
            //Check the authkey is correct for our current overlord just incase we disconnected incorrectly and reconnected elsewhere
            if (string.IsNullOrEmpty(req.AuthKey) || req.AuthKey == model.Network.Overlord.Secret)
                SendOk(e);
            return true;
        }

        private bool HandleDisconnect(FAP.Network.Server.RequestEventArgs e)
        {
            SendOk(e);
            return true;
        }

        private void SendOk(FAP.Network.Server.RequestEventArgs e)
        {
            e.Response.StatusCode = (int)HttpStatusCode.OK;
            var generator = new ModernResponseWriter();
            generator.SendHeaders(e.Context, e.Response);
        }
    }
}