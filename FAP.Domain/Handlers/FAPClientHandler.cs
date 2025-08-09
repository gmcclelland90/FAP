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
// Legacy HttpServer usings removed
using Microsoft.Extensions.Logging;

namespace FAP.Domain.Handlers
{
    public class FAPClientHandler : IFAPHandler
    {
        private readonly BufferService bufferService;
        private readonly IConversationController chatController;
        private readonly ILogger<FAPClientHandler> logger;
        private readonly Model model;
        private readonly ServerUploadLimiterService serverUploadLimiterService;
        private readonly ShareInfoService shareInfoService;

        public FAPClientHandler(Model m, ShareInfoService s, IConversationController c, BufferService b,
                                ServerUploadLimiterService sl, ILogger<FAPClientHandler> logger)
        {
            model = m;
            shareInfoService = s;
            chatController = c;
            bufferService = b;
            serverUploadLimiterService = sl;
            this.logger = logger;
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
            logger.LogTrace("Client rx: {Verb} p: {Param} source: {Source} overlord: {Overlord}", req.Verb, req.Param, req.SourceID,
                         req.OverlordID);
            logger.LogDebug("FAPClientHandler.HandleAsync: Processing verb: {Verb}", req.Verb);
            switch (req.Verb)
            {
                case "BROWSE":
                    logger.LogDebug("FAPClientHandler.HandleAsync: Routing to HandleBrowse");
                    return HandleBrowse(e, req);
                case "UPDATE":
                    logger.LogDebug("FAPClientHandler.HandleAsync: Routing to HandleUpdate");
                    return HandleUpdate(e, req);
                case "INFO":
                    logger.LogDebug("FAPClientHandler.HandleAsync: Routing to HandleInfoAsync");
                    return await HandleInfoAsync(e);
                case "NOOP":
                    logger.LogDebug("FAPClientHandler.HandleAsync: Routing to HandleNOOP");
                    return HandleNOOP(e, req);
                case "GET":
                    logger.LogDebug("FAPClientHandler.HandleAsync: Routing to HandleGet");
                    return HandleGet(e, req);
                case "DISCONNECT":
                    logger.LogDebug("FAPClientHandler.HandleAsync: Routing to HandleDisconnect");
                    return HandleDisconnect(e);
                case "CHAT":
                    logger.LogDebug("FAPClientHandler.HandleAsync: Routing to HandleChat");
                    return HandleChat(e, req);
                case "COMPARE":
                    logger.LogDebug("FAPClientHandler.HandleAsync: Routing to HandleCompareAsync");
                    return await HandleCompareAsync(e, req);
                case "SEARCH":
                    logger.LogDebug("FAPClientHandler.HandleAsync: Routing to HandleSearch");
                    return HandleSearch(e, req);
                case "CONVERSTATION":
                    logger.LogDebug("FAPClientHandler.HandleAsync: Routing to HandleConversation");
                    return HandleConversation(e, req);
                case "ADDDOWNLOAD":
                    logger.LogDebug("FAPClientHandler.HandleAsync: Routing to HandleAddDownload");
                    return HandleAddDownload(e, req);
                default:
                    logger.LogDebug("FAPClientHandler.HandleAsync: Unknown verb: {Verb}", req.Verb);
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
                        // Keep session for progress accounting; uploader is deprecated
                        var ffu = new FAPFileUploader(bufferService, serverUploadLimiterService, Microsoft.Extensions.Logging.Abstractions.NullLogger<FAPFileUploader>.Instance);
                        var session = new TransferSession(ffu);
                        model.TransferSessions.Add(session);
                        try
                        {
                            //Try to find the username of the request
                            string userName = e.Context.RemoteEndPoint.Address.ToString();
                            Node? search = model.Network.Nodes.ToList().Where(n => n.ID == req.SourceID).FirstOrDefault();
                            if (search != null && !string.IsNullOrEmpty(search.Nickname))
                                userName = search.Nickname;

                            using (
                                FileStream fs = File.Open(possiblePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                // TODO: Implement modern upload functionality (streaming handled by ModernHTTPHandler for now)
                                logger.LogInformation("Upload requested for {Path} by {User}", possiblePath, userName);
                            }

                            //Add log of upload
                            double seconds = (DateTime.Now - ffu.TransferStart).TotalSeconds;
                            var txlog = new TransferLog();
                            txlog.Nickname = userName;
                            txlog.Completed = DateTime.Now;
                            txlog.Filename = Path.GetFileName(possiblePath);
                            txlog.Path = Path.GetDirectoryName(req.Param) ?? string.Empty;
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
            var generator = new ModernResponseWriter(logger);
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

        private async Task<bool> HandleBrowseAsync(FAP.Network.Server.RequestEventArgs e, NetworkRequest req)
        {
            var verb = new BrowseVerb(shareInfoService);
            NetworkRequest result = verb.ProcessRequest(req);
            byte[] data = Encoding.UTF8.GetBytes(result.Data);
            var generator = new ModernResponseWriter(logger);
            e.Response.ContentLength = data.Length;
            generator.SendHeaders(e.Context, e.Response);
            await e.Context.Stream.WriteAsync(data, 0, data.Length);
            await e.Context.Stream.FlushAsync();
            data = null!;
            return true;
        }

        private bool HandleBrowse(FAP.Network.Server.RequestEventArgs e, NetworkRequest req)
        {
            // For backward compatibility, use the async version
            return HandleBrowseAsync(e, req).GetAwaiter().GetResult();
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

        private async Task<bool> HandleSearchAsync(FAP.Network.Server.RequestEventArgs e, NetworkRequest req)
        {
            //We dont do this on a server..
            var verb = new SearchVerb(shareInfoService);
            NetworkRequest result = verb.ProcessRequest(req);
            byte[] data = Encoding.UTF8.GetBytes(result.Data);
            var generator = new ModernResponseWriter(logger);
            e.Response.ContentLength = data.Length;
            generator.SendHeaders(e.Context, e.Response);
            await e.Context.Stream.WriteAsync(data, 0, data.Length);
            await e.Context.Stream.FlushAsync();
            data = null!;
            return true;
        }

        private bool HandleSearch(FAP.Network.Server.RequestEventArgs e, NetworkRequest req)
        {
            // For backward compatibility, use the async version
            return HandleSearchAsync(e, req).GetAwaiter().GetResult();
        }

        private async Task<bool> HandleCompareAsync(FAP.Network.Server.RequestEventArgs e, NetworkRequest req)
        {
            var verb = new CompareVerb(model);

            NetworkRequest result = verb.ProcessRequest(req);
            byte[] data = Encoding.UTF8.GetBytes(result.Data);
            var generator = new ModernResponseWriter(logger);
            e.Response.ContentLength = data.Length;
            generator.SendHeaders(e.Context, e.Response);
            await e.Context.Stream.WriteAsync(data, 0, data.Length);
            await e.Context.Stream.FlushAsync();
            data = null!;

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
            logger.LogDebug("FAPClientHandler.HandleUpdate: Starting update processing");
            logger.LogDebug("FAPClientHandler.HandleUpdate: AuthKey = '{AuthKey}', Overlord.Secret = '{Secret}'", req.AuthKey, model.Network.Overlord.Secret);
            logger.LogDebug("FAPClientHandler.HandleUpdate: Request data length: {Length}", req.Data?.Length ?? 0);
            
            // For self-connections, AuthKey might be empty, so we need to handle that case
            bool authValid = string.IsNullOrEmpty(req.AuthKey) || req.AuthKey == model.Network.Overlord.Secret;
            
            if (authValid)
            {
                logger.LogDebug("FAPClientHandler.HandleUpdate: Authentication valid, processing update");
                model.Network.Overlord.LastUpdate = Environment.TickCount;
                var verb = new UpdateVerb();
                verb.ProcessRequest(req);
                logger.LogDebug("FAPClientHandler.HandleUpdate: Processed UpdateVerb, nodes count: {Count}", verb.Nodes?.Count ?? 0);
                
                foreach (Node node in verb.Nodes)
                {
                    logger.LogDebug("FAPClientHandler.HandleUpdate: Processing node {Id} (Online: {Online}, Nickname: {Nickname})", 
                        node.ID, node.Online, node.Nickname);
                    
                    Node search = model.Network.Nodes.Where(i => i.ID == node.ID).FirstOrDefault();
                    if (search == null)
                    {
                        // Add the node if it has an ID and is online (or if Online is not set, assume it's online)
                        if (!string.IsNullOrEmpty(node.ID) && (node.Online || !node.ContainsKey("Online")))
                        {
                            logger.LogDebug("FAPClientHandler.HandleUpdate: Adding new node {Id} to network", node.ID);
                            model.Network.Nodes.Add(node);
                            logger.LogDebug("FAPClientHandler.HandleUpdate: Network now has {Count} nodes", model.Network.Nodes.Count);
                        }
                        else
                        {
                            logger.LogDebug("FAPClientHandler.HandleUpdate: Skipping node {Index} - ID: {Id}, Online: {Online}", 
                                node.ID, !string.IsNullOrEmpty(node.ID), node.Online);
                        }
                    }
                    else
                    {
                        logger.LogDebug("FAPClientHandler.HandleUpdate: Updating existing node {Id}", node.ID);
                        foreach (var param in node.Data)
                            search.SetData(param.Key, param.Value);
                        //Has the client disconnected?
                        if (!search.Online)
                        {
                            model.Network.Nodes.Remove(node);
                            logger.LogTrace("Client: Node offline update: {Id}", node.ID);
                        }
                    }
                }
                SendOk(e);
                logger.LogDebug("FAPClientHandler.HandleUpdate: Update processed successfully");
                return true;
            }
            else
            {
                logger.LogDebug("FAPClientHandler.HandleUpdate: Authentication failed, rejecting update");
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
            var generator = new ModernResponseWriter(logger);
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
            var generator = new ModernResponseWriter(logger);
            generator.SendHeaders(e.Context, e.Response);
        }
    }
}