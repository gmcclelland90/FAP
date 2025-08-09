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
using FAP.Network.Server; // Use modern HTTP context wrapper
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

        public void DoUpload(ModernHttpContext context, Stream stream, string user, string url)
        {
            // Legacy HTTP uploader depended on HttpServer abstractions; not supported in modern server
            throw new NotSupportedException("Legacy HTTP uploader (HttpServer) is not supported in the modern server.");
        }
    }
}