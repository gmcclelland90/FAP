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
using System.Text;
using FAP.Network.Entities;
using FAP.Network.Server;
// Legacy HttpServer references removed
using Microsoft.Extensions.Logging;

namespace FAP.Network
{
    public class Multiplexor
    {
        private static readonly string preample = "/Fap.app/";
        private static ILogger<Multiplexor>? staticLogger;
        public static void InitializeLogger(ILogger<Multiplexor> logger) => staticLogger = logger;

        public static string Encode(string url, string verb, string param)
        {
            var sb = new StringBuilder();
            if (!url.StartsWith("http://"))
                sb.Append("http://");
            sb.Append(url);
            if (url.EndsWith("/"))
                sb.Append(preample.Substring(1));
            else
                sb.Append(preample);
            sb.Append(verb);
            if (!string.IsNullOrEmpty(param))
            {
                sb.Append("?p=");
                sb.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(param)).Replace('+', '_'));
            }
            string result = sb.ToString();
            sb.Length = 0;
            sb = null;
            return result;
        }

        // Legacy Decode(IRequest) and GetPostString(IRequest) removed

        public static async Task<NetworkRequest> DecodeModernAsync(ModernHttpRequest r)
        {
            var req = new NetworkRequest();
            if (!r.Path.StartsWith(preample))
                throw new Exception("Malformed url");
            req.Verb = r.Path.Substring(preample.Length);

            // Extract parameter from query string
            if (r.Query.ContainsKey("p"))
            {
                req.Param = Encoding.UTF8.GetString(Convert.FromBase64String(r.Query["p"].ToString().Replace('_', '+')));
            }
            if (r.Method == "POST")
            {
                staticLogger?.LogTrace("Multiplexor.DecodeModernAsync: Reading POST body");
                req.Data = await GetPostStringModernAsync(r);
                staticLogger?.LogTrace("Multiplexor.DecodeModernAsync: POST body read length={Length}", req.Data?.Length ?? 0);
            }
            else
            {
                staticLogger?.LogTrace("Multiplexor.DecodeModernAsync: Handling non-POST {Method}", r.Method);
            }

            // Extract headers
            if (r.Headers.ContainsKey("FAP-AUTH"))
            {
                req.AuthKey = r.Headers["FAP-AUTH"].ToString();
            }
            if (r.Headers.ContainsKey("FAP-SOURCE"))
            {
                req.SourceID = r.Headers["FAP-SOURCE"].ToString();
            }
            if (r.Headers.ContainsKey("FAP-OVERLORD"))
            {
                req.OverlordID = r.Headers["FAP-OVERLORD"].ToString();
            }

            return req;
        }


        public static async Task<string> GetPostStringModernAsync(ModernHttpRequest e)
        {
            try
            {
                if (e.Body == null)
                {
                    staticLogger?.LogWarning("Multiplexor.GetPostStringModernAsync: Body was null");
                    return string.Empty;
                }

                // Check if the body is seekable and reset position if needed
                if (e.Body.CanSeek)
                {
                    e.Body.Position = 0;
                }

                using (var reader = new StreamReader(e.Body, Encoding.UTF8, leaveOpen: true))
                {
                    string content = await reader.ReadToEndAsync();
                    staticLogger?.LogTrace("Multiplexor.GetPostStringModernAsync: Read {Length} bytes", content?.Length ?? 0);
                    return content;
                }
            }
            catch (Exception ex)
            {
                // Log the error but return empty string to avoid breaking the flow
                staticLogger?.LogError(ex, "Multiplexor.GetPostStringModernAsync: Error reading request body");
                return string.Empty;
            }
        }

    }
}