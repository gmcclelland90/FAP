using System;
using System.Buffers;
using System.IO;
using System.Text;
using FAP.Network.Server;
using Microsoft.Extensions.Logging;

namespace FAP.Domain.Handlers
{
    /// <summary>
    /// Modern response writer for ASP.NET Core-based HTTP handling
    /// </summary>
    public class ModernResponseWriter
    {
        private readonly ILogger logger;

        public ModernResponseWriter()
        {
            logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        }

        public ModernResponseWriter(ILogger logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Sends headers to the client
        /// </summary>
        /// <param name="context">HTTP context</param>
        /// <param name="response">Response object</param>
        public void SendHeaders(ModernHttpContext context, ModernHttpResponse response)
        {
            try
            {
                // Set content type if not already set
                if (string.IsNullOrEmpty(response.ContentType))
                {
                    response.ContentType = "text/plain";
                }

                // Set content length if not already set
                if (!response.ContentLength.HasValue)
                {
                    response.ContentLength = 0;
                }

                // Add any custom headers here if needed
                // For now, we rely on the ASP.NET Core pipeline to handle headers
            }
            catch (Exception ex)
            {
                // Log error but don't throw
                logger.LogError(ex, "ModernResponseWriter.SendHeaders: Error sending headers");
            }
        }

        /// <summary>
        /// Sends a string response
        /// </summary>
        /// <param name="context">HTTP context</param>
        /// <param name="data">String data to send</param>
        /// <param name="encoding">Encoding to use</param>
        public async Task SendAsync(ModernHttpContext context, string data, Encoding encoding)
        {
            try
            {
                int maxByteCount = encoding.GetMaxByteCount(data.Length);
                byte[] buffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
                try
                {
                    int bytesWritten = encoding.GetBytes(data, 0, data.Length, buffer, 0);
                    
                    // Set content length before writing
                    var response = context.AspNetCoreContext.Response;
                    response.ContentLength = bytesWritten;
                    
                    await context.Stream.WriteAsync(buffer.AsMemory(0, bytesWritten));
                    await context.Stream.FlushAsync();
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ModernResponseWriter.SendAsync(string): Error sending data");
            }
        }

        /// <summary>
        /// Sends a byte array response
        /// </summary>
        /// <param name="context">HTTP context</param>
        /// <param name="data">Byte array to send</param>
        public async Task SendAsync(ModernHttpContext context, byte[] data)
        {
            try
            {
                if (data != null && data.Length > 0)
                {
                    // Set content length before writing
                    var response = context.AspNetCoreContext.Response;
                    response.ContentLength = data.Length;
                    
                    await context.Stream.WriteAsync(data.AsMemory(0, data.Length));
                    await context.Stream.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ModernResponseWriter.SendAsync(byte[]): Error sending data");
            }
        }
    }
} 