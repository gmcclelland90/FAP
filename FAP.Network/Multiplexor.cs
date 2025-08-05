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
using HttpServer;
using HttpServer.Headers;
using NLog;

namespace FAP.Network
{
    public class Multiplexor
    {
        private static readonly string preample = "/Fap.app/";
        private static readonly Logger logger = LogManager.GetLogger("faplog");

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

        public static NetworkRequest Decode(IRequest r)
        {
            var req = new NetworkRequest();
            if (!r.Uri.AbsolutePath.StartsWith(preample))
                throw new Exception("Malformed url");
            req.Verb = r.Uri.AbsolutePath.Substring(preample.Length);

            IParameter param = r.Parameters.Where(p => p.Name == "p").FirstOrDefault();
            if (null != param)
            {
                req.Param = Encoding.UTF8.GetString(Convert.FromBase64String(param.Value.Replace('_', '+')));
            }
            if (r.Method == "POST")
            {
                req.Data = GetPostString(r);
            }

            var headers = r.Headers as HeaderCollection;
            if (null != headers)
            {
                foreach (IHeader  h in headers)
                {
                    var header = h as StringHeader;
                    if (null != header)
                    {
                        switch (header.Name.ToUpper())
                        {
                            case "FAP-AUTH":
                                req.AuthKey = header.Value;
                                break;
                            case "FAP-SOURCE":
                                req.SourceID = header.Value;
                                break;
                            case "FAP-OVERLORD":
                                req.OverlordID = header.Value;
                                break;
                        }
                    }
                }
            }
            return req;
        }


        public static string GetPostString(IRequest e)
        {
            using (var reader = new StreamReader(e.Body, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

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
                logger.Debug($"DecodeModern: Processing POST request for verb: {req.Verb}");
                req.Data = await GetPostStringModernAsync(r);
                logger.Debug($"DecodeModern: Request data length: {req.Data?.Length ?? 0}");
            }
            else
            {
                logger.Debug($"DecodeModern: Processing {r.Method} request for verb: {req.Verb}");
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

        public static NetworkRequest DecodeModern(ModernHttpRequest r)
        {
            // For backward compatibility, we'll use a synchronous wrapper
            // but this should be avoided in new code
            try
            {
                return DecodeModernAsync(r).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                logger.Debug($"Error in synchronous wrapper: {ex.Message}");
                return new NetworkRequest();
            }
        }

        public static async Task<string> GetPostStringModernAsync(ModernHttpRequest e)
        {
            try
            {
                if (e.Body == null)
                {
                    logger.Debug("GetPostStringModern: Request body is null");
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
                    logger.Debug($"GetPostStringModern: Read {content.Length} characters from request body");
                    return content;
                }
            }
            catch (Exception ex)
            {
                // Log the error but return empty string to avoid breaking the flow
                logger.Debug($"Error reading request body: {ex.Message}");
                return string.Empty;
            }
        }

        public static string GetPostStringModern(ModernHttpRequest e)
        {
            // For backward compatibility, we'll use a synchronous wrapper
            // but this should be avoided in new code
            try
            {
                return GetPostStringModernAsync(e).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                logger.Debug($"Error in synchronous wrapper: {ex.Message}");
                return string.Empty;
            }
        }
    }
}