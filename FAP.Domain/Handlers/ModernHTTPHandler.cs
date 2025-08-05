using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FAP.Domain.Entities;
using FAP.Domain.Services;
using FAP.Network.Server;
using Fap.Foundation;
using NLog;

namespace FAP.Domain.Handlers
{
    public class ModernHTTPHandler
    {
        private const string WEB_PREFIX = "/Fap.app.web/";
        private const string WEB_ICON_PREFIX = "/Fap.app.web/icon/";

        private readonly ShareInfoService infoService;
        private readonly Model model;
        private readonly BufferService bufferService;
        private readonly ServerUploadLimiterService uploadLimiter;
        private readonly Logger logger;

        public ModernHTTPHandler(ShareInfoService i, Model m, BufferService b, ServerUploadLimiterService u)
        {
            infoService = i;
            model = m;
            bufferService = b;
            uploadLimiter = u;
            logger = LogManager.GetLogger("faplog");
        }

        public bool Handle(string path, RequestEventArgs e)
        {
            try
            {
                e.Response.StatusCode = 200;
                string decodedPath = Utility.DecodeURL(path);
                byte[] data = null;

                if (decodedPath.StartsWith(WEB_ICON_PREFIX))
                {
                    // Handle icon requests
                    string ext = decodedPath.Substring(decodedPath.LastIndexOf("/") + 1);
                    data = GetResource($"Images\\{ext}.png");
                    e.Response.ContentType = "image/png";
                }
                else if (decodedPath.StartsWith(WEB_PREFIX))
                {
                    // Handle static file requests
                    data = GetResource(decodedPath.Substring(WEB_PREFIX.Length));
                    
                    string ext = Path.GetExtension(decodedPath);
                    if (!string.IsNullOrEmpty(ext) && ext.StartsWith("."))
                        ext = ext.Substring(1);

                    e.Response.ContentType = GetContentType(ext);
                }
                else
                {
                    // Handle main page
                    data = GetResource("template.html");
                    e.Response.ContentType = "text/html";
                }

                if (data != null)
                {
                    e.Response.Body.Write(data, 0, data.Length);
                    e.IsHandled = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error handling HTTP request");
                e.Response.StatusCode = 500;
                e.IsHandled = true;
            }

            return false;
        }

        private string GetContentType(string extension)
        {
            return extension?.ToLower() switch
            {
                "html" => "text/html",
                "css" => "text/css",
                "js" => "application/javascript",
                "png" => "image/png",
                "jpg" => "image/jpeg",
                "jpeg" => "image/jpeg",
                "ico" => "image/x-icon",
                _ => "application/octet-stream"
            };
        }

        private byte[] GetResource(string name)
        {
            try
            {
                var assembly = typeof(ModernHTTPHandler).Assembly;
                var resourceName = $"FAP.Domain.Web.Resources.{name.Replace('\\', '.')}";
                
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var memoryStream = new MemoryStream();
                    stream.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to get resource: {name}");
            }

            return new byte[0];
        }
    }
} 