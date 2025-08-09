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
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using FAP.Domain.Entities;
using FAP.Domain.Net;
using FAP.Domain.Services;
using Fap.Foundation;
using HttpServer;
using HttpServer.Headers;
using HttpServer.Messages;
using Microsoft.Extensions.Logging;

namespace FAP.Domain.Handlers
{
    public class HTTPFileUploader : ITransferWorker
    {
        private readonly BufferService bufferService;
        private readonly ILogger<HTTPFileUploader> logger;
        private readonly NetworkSpeedMeasurement nsm;
        private readonly ServerUploadLimiterService uploadLimiter;

        private bool isComplete;
        private long length;
        private long position;
        private string status = "HTTP - Connecting..";

        public HTTPFileUploader(BufferService b, ServerUploadLimiterService u, ILogger<HTTPFileUploader> logger)
        {
            bufferService = b;
            uploadLimiter = u;
            this.logger = logger;
            nsm = new NetworkSpeedMeasurement(NetSpeedType.Upload);
        }

        public DateTime TransferStart { set; get; }
        public long ResumePoint { set; get; }

        #region ITransferWorker Members

        public long Length
        {
            get { return length; }
        }

        public bool IsComplete
        {
            get { return isComplete; }
        }

        public long Speed
        {
            get { return nsm.GetSpeed(); }
        }

        public string Status
        {
            get { return status; }
        }

        public long Position
        {
            get { return position; }
        }

        #endregion

        public void DoUpload(IHttpContext context, Stream stream, string user, string url)
        {
            length = stream.Length;
            ResumePoint = 0;
            IHeader? rangeHeader =
                context.Request.Headers.Where(n => n.Name.ToLowerInvariant() == "range").FirstOrDefault();
            ServerUploadToken? token = null;
            var sw = Stopwatch.StartNew();
            using var scope = logger.BeginScope(new Dictionary<string, object>
            {
                ["Protocol"] = "HTTP",
                ["User"] = user,
                ["Url"] = url,
                ["Size"] = stream.Length
            });
            logger.LogInformation("Upload started: {Protocol} {User} {Url} size={Size}", "HTTP", user, url, stream.Length);
            try
            {
                if (stream.Length > Model.FREE_FILE_LIMIT)
                {
                    //File isnt free leech, acquire a token before we send the file
                    token = uploadLimiter.RequestUploadToken(context.RemoteEndPoint.Address.ToString());
                    while (token.GlobalQueuePosition > 0)
                    {
                        context.LastAction = DateTime.Now;
                        status = $"HTTP ({user}) queued upload in slot {token.GlobalQueuePosition}";
                        token.WaitTimeout();
                    }
                }

                status = $"HTTP ({user}) Sending {url}";
                try
                {
                    if (null != rangeHeader)
                    {
                        //Partial request
                        //Try to parse header - if we fail just send a 200 ok from zero
                        long start = 0;
                        long end = 0;

                        if (rangeHeader.HeaderValue.StartsWith("bytes="))
                        {
                            string header = rangeHeader.HeaderValue.Substring(6).Trim();
                            string starttxt = string.Empty;
                            string endtxt = string.Empty;

                            if (header.Contains('-'))
                            {
                                starttxt = header.Substring(0, header.IndexOf("-"));
                                endtxt = header.Substring(header.IndexOf("-") + 1,
                                                          header.Length - (header.IndexOf("-") + 1));
                            }
                            else
                            {
                                starttxt = header;
                            }

                            if (!string.IsNullOrEmpty(starttxt))
                                start = long.Parse(starttxt);
                            if (!string.IsNullOrEmpty(endtxt))
                                end = long.Parse(endtxt);
                            //Only allow a partial request start.  May implement this at some point but its beyond the scope of this atm.
                            if (start != 0 && end == 0)
                            {
                                if (start > stream.Length)
                                    start = stream.Length;
                                stream.Seek(start, SeekOrigin.Begin);
                                position = start;

                                ResumePoint = start;
                            }
                            context.Response.Status = HttpStatusCode.PartialContent;
                        }
                    }
                }
                catch
                {
                }

                TransferStart = DateTime.Now;
                //Send headers
                context.Response.ContentLength.Value = stream.Length - stream.Position;
                var generator = new ResponseWriter();
                generator.SendHeaders(context, context.Response);
                //Send data
                MemoryBuffer buffer = bufferService.GetBuffer();
                try
                {
                    int bytesRead = stream.Read(buffer.Data, 0, buffer.Data.Length);
                    while (bytesRead > 0)
                    {
                        context.LastAction = DateTime.Now;
                        context.Stream.Write(buffer.Data, 0, bytesRead);
                        position += bytesRead;
                        nsm.PutData(bytesRead);
                        bytesRead = stream.Read(buffer.Data, 0, buffer.Data.Length);
                    }
                }
                catch (Exception err)
                {
                    logger.LogTrace(err, "HTTPFileUploader.DoUpload: Error while uploading to {User} {Url}", user, url);
                }
                finally
                {
                    bufferService.FreeBuffer(buffer);
                }
            }
            finally
            {
                sw.Stop();
                status = $"HTTP ({user}) Upload complete";
                if (null != token)
                    uploadLimiter.FreeToken(token);
                isComplete = true;
                position = length;
                var bytes = Math.Max(0, length - ResumePoint);
                var elapsedMs = Math.Max(1, sw.ElapsedMilliseconds);
                var kbps = (bytes / 1024.0) / (elapsedMs / 1000.0);
                logger.LogInformation("Upload completed: {Protocol} {User} {Url} bytes={Bytes} durationMs={DurationMs} avgKbps={Kbps}",
                    "HTTP", user, url, bytes, elapsedMs, kbps);
            }
        }
    }
}