using System;
using System.IO;
using System.Text;
using FAP.Network.Server;

namespace FAP.Domain.Handlers
{
    /// <summary>
    /// Modern response writer for ASP.NET Core-based HTTP handling
    /// </summary>
    public class ModernResponseWriter
    {
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
                Console.WriteLine($"Error sending headers: {ex.Message}");
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
                byte[] buffer = encoding.GetBytes(data);
                
                // Set content length before writing
                var response = context.AspNetCoreContext.Response;
                response.ContentLength = buffer.Length;
                
                await context.Stream.WriteAsync(buffer, 0, buffer.Length);
                await context.Stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending data: {ex.Message}");
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
                    
                    await context.Stream.WriteAsync(data, 0, data.Length);
                    await context.Stream.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending data: {ex.Message}");
            }
        }
    }
} 