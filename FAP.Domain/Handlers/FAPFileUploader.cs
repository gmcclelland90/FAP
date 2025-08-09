using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using FAP.Domain.Entities;
using FAP.Domain.Net;
using FAP.Domain.Services;
using Fap.Foundation;
using FAP.Network.Server; // Use modern HTTP context wrapper
using Microsoft.Extensions.Logging;

namespace FAP.Domain.Handlers
{
    public class FAPFileUploader : ITransferWorker
    {
        private static readonly byte[] CRLF = Encoding.ASCII.GetBytes("\r\n");
        private static readonly int CHUNK_SIZE_LIMIT = 2000000000; //1.86gb
        private readonly BufferService bufferService;
        private readonly ILogger<FAPFileUploader> logger;
        private readonly NetworkSpeedMeasurement nsm;
        private readonly ServerUploadLimiterService uploadLimiter;

        private bool isComplete;
        private long length;
        private long position;
        private string status = "FAP Upload - Connecting..";

        public FAPFileUploader(BufferService b, ServerUploadLimiterService u, ILogger<FAPFileUploader> logger)
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

        public void DoUpload(ModernHttpContext context, Stream stream, string user, string url)
        {
            // Legacy upload path not supported in modern server. Keep stub to satisfy interface users.
            throw new NotSupportedException("Legacy FAP chunked upload (HttpServer) is not supported in the modern server.");
        }

        private void SendChunkedData(ModernHttpContext context, byte[] data) => throw new NotSupportedException();


        /// <summary>
        /// Send custom headers to the client.
        /// </summary>
        /// <param name="response">Response containing call headers.</param>
        /// <param name="context">Content used to send headers.</param>
        private void SendChunkedHeaders(ModernHttpContext context) => throw new NotSupportedException();
    }
}