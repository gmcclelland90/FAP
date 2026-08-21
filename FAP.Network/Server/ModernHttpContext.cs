using System;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace FAP.Network.Server
{
    /// <summary>
    /// Modern HTTP context that wraps ASP.NET Core's HttpContext.
    /// </summary>
    public class ModernHttpContext
    {
        private readonly HttpContext _context;

        public ModernHttpContext(HttpContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets the remote endpoint.
        /// </summary>
        public IPEndPoint RemoteEndPoint => _context.Connection.RemoteIpAddress != null 
            ? new IPEndPoint(_context.Connection.RemoteIpAddress, _context.Connection.RemotePort) 
            : new IPEndPoint(IPAddress.Any, 0);

        /// <summary>
        /// Gets the stream for reading/writing.
        /// </summary>
        public Stream Stream => _context.Response.Body;

        /// <summary>
        /// Helper to enable request buffering if handlers need to re-read the body.
        /// </summary>
        public void EnableRequestBuffering()
        {
            try
            {
                _context.Request.EnableBuffering();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Gets the underlying ASP.NET Core HttpContext.
        /// </summary>
        public HttpContext AspNetCoreContext => _context;
    }
} 