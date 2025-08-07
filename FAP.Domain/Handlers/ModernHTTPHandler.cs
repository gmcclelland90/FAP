using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FAP.Domain.Entities;
using FAP.Domain.Entities.FileSystem;
using FAP.Domain.Services;
using FAP.Network.Server;
using Fap.Foundation;
using NLog;
using Directory = FAP.Domain.Entities.FileSystem.Directory;
using File = System.IO.File;
using System.Drawing;
using System.Drawing.Imaging;

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

        // Icon cache - missing from original ModernHTTPHandler
        private readonly Dictionary<string, byte[]> iconCache = new Dictionary<string, byte[]>();
        private readonly object sync = new object();

        public ModernHTTPHandler(ShareInfoService i, Model m, BufferService b, ServerUploadLimiterService u)
        {
            infoService = i;
            model = m;
            bufferService = b;
            uploadLimiter = u;
            logger = LogManager.GetLogger("faplog");
        }

        public async Task<bool> HandleAsync(string path, RequestEventArgs e)
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
                    return await SendIconAsync(e, ext);
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
                    // Handle main page with template processing
                    string page = Encoding.UTF8.GetString(GetResource("template.html"));
                    var pagedata = new Dictionary<string, object>();

                    pagedata.Add("model", model);
                    pagedata.Add("appver", Model.AppVersion);
                    pagedata.Add("freelimit", Utility.FormatBytes(Model.FREE_FILE_LIMIT));
                    pagedata.Add("uploadslots", model.MaxUploads);
                    int freeslots = model.MaxUploads - uploadLimiter.GetActiveTokenCount();
                    pagedata.Add("currentuploadslots", freeslots);
                    pagedata.Add("queueInfo", freeslots > 0 ? "" : "  Queue length: " + uploadLimiter.GetQueueLength() + ".");
                    pagedata.Add("slotcolour", freeslots > 0 ? "green" : "red");

                    pagedata.Add("util", new Utility());

                    if (!decodedPath.EndsWith("/"))
                        pagedata.Add("path", (decodedPath + "/").Replace("#", "%23"));
                    else
                        pagedata.Add("path", (decodedPath).Replace("#", "%23"));

                    //Add path info
                    var paths = new List<Dictionary<string, object>>();

                    string[] split = decodedPath.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < split.Length; i++)
                    {
                        var sb = new StringBuilder("/");
                        for (int y = 0; y <= i; y++)
                        {
                            sb.Append(split[y]);
                            sb.Append("/");
                        }

                        var di = new Dictionary<string, object>();
                        di.Add("Name", split[i]);
                        di.Add("Path", sb.ToString());
                        paths.Add(di);
                    }

                    pagedata.Add("pathSplit", paths);

                    var files = new List<Dictionary<string, object>>();
                    long totalSize = 0;

                    //Try to resolve the path to a file first
                    string[] possiblePaths;
                    if (infoService.ToLocalPath(decodedPath, out possiblePaths))
                        //User has requested a file
                        foreach (string possiblePath in possiblePaths)
                            if (File.Exists(possiblePath))
                                return await SendFileAsync(e, possiblePath, decodedPath);

                    //User didnt request a file so try to send directory info
                    List<BrowsingFile> results;
                    if (infoService.GetPath(decodedPath, false, true, out results))
                    {
                        foreach (var browsingFile in results)
                        {
                            if (browsingFile.IsFolder)
                            {
                                // Construct the complete folder icon HTML tag
                                string folderIconHtml = $"<img height=\"16px\" width=\"16px\" src=\"{WEB_ICON_PREFIX}folder\" alt=\"icon\" />";
                                
                                var d = new Dictionary<string, object>
                                            {
                                                {"Name", browsingFile.Name},
                                                {"Path", Utility.EncodeURL(browsingFile.Name)},
                                                {"Icon", "folder"},
                                                {"HasIcon", true}, // Folder icon always exists
                                                {"IconHtml", folderIconHtml}, // Pre-built HTML tag
                                                {"Sizetxt", Utility.FormatBytes(browsingFile.Size)},
                                                {"Size", browsingFile.Size},
                                                {
                                                    "LastModifiedtxt",
                                                    browsingFile.LastModified.ToShortDateString()
                                                    },
                                                {"LastModified", browsingFile.LastModified}
                                            };
                                files.Add(d);
                                totalSize += browsingFile.Size;
                            }
                            else
                            {
                                var d = new Dictionary<string, object> {{"Name", browsingFile.Name}};
                                string ext = Path.GetExtension(browsingFile.Name);
                                if (ext != null && ext.StartsWith("."))
                                    ext = ext.Substring(1);
                                string name = browsingFile.Name;
                                if (!string.IsNullOrEmpty(name))
                                    name = name.Replace("#", "%23");
                                d.Add("Path", name);
                                d.Add("Icon", ext);
                                
                                // Construct the complete file icon HTML tag
                                string fileIconHtml = "";
                                if (!string.IsNullOrEmpty(ext))
                                {
                                    fileIconHtml = $"<img height=\"16px\" width=\"16px\" src=\"{WEB_ICON_PREFIX}{ext}\" alt=\"icon\" />";
                                }
                                d.Add("IconHtml", fileIconHtml);
                                
                                // Check if icon exists (static file or can be generated)
                                bool hasIcon = false;
                                if (!string.IsNullOrEmpty(ext))
                                {
                                    // Check if static icon file exists
                                    var staticIconData = GetResource($"Images/{ext}.png");
                                    if (staticIconData.Length > 0)
                                    {
                                        hasIcon = true;
                                    }
                                    else
                                    {
                                        // For now, assume we can generate icons for all file types
                                        // In a more sophisticated implementation, we could check if the extension is valid
                                        hasIcon = true;
                                    }
                                }
                                d.Add("HasIcon", hasIcon);
                                
                                d.Add("Size", browsingFile.Size);
                                d.Add("Sizetxt", Utility.FormatBytes(browsingFile.Size));
                                d.Add("LastModifiedtxt", browsingFile.LastModified.ToShortDateString());
                                d.Add("LastModified", browsingFile.LastModified);
                                files.Add(d);
                                totalSize += browsingFile.Size;
                            }
                        }

                        //Clear result list to help GC
                        results.Clear();
                    }

                    pagedata.Add("files", files);
                    pagedata.Add("totalSize", Utility.FormatBytes(totalSize));

                    // Debug: Log the data being passed to template engine
                    logger.Debug($"ModernHTTPHandler: Template data contains {pagedata.Count} items:");
                    foreach (var kvp in pagedata)
                    {
                        logger.Debug($"ModernHTTPHandler: {kvp.Key} = {kvp.Value?.GetType().Name ?? "null"}");
                    }

                    // Debug: Log the files data structure
                    if (files.Count > 0)
                    {
                        logger.Debug($"ModernHTTPHandler: Files count = {files.Count}");
                        foreach (var file in files)
                        {
                            logger.Debug($"ModernHTTPHandler: File data:");
                            foreach (var kvp in file)
                            {
                                logger.Debug($"ModernHTTPHandler:   {kvp.Key} = {kvp.Value}");
                            }
                        }
                    }

                    // Debug: Log the template before processing
                    logger.Debug($"ModernHTTPHandler: Template before processing (first 500 chars): {page.Substring(0, Math.Min(500, page.Length))}");

                    // Process the template
                    logger.Debug("ModernHTTPHandler: About to call TemplateEngine.Generate");
                    page = TemplateEngine.Generate(page, pagedata);
                    logger.Debug("ModernHTTPHandler: TemplateEngine.Generate completed");
                    
                    // Debug: Log the template after processing
                    logger.Debug($"ModernHTTPHandler: Template after processing (first 500 chars): {page.Substring(0, Math.Min(500, page.Length))}");
                    logger.Debug($"ModernHTTPHandler: Full template after processing: {page}");

                    data = Encoding.UTF8.GetBytes(page);
                    e.Response.ContentType = "text/html";
                }

                if (data != null)
                {
                    await e.Response.Body.WriteAsync(data, 0, data.Length);
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

        public bool Handle(string path, RequestEventArgs e)
        {
            // For backward compatibility, use the async version
            return HandleAsync(path, e).GetAwaiter().GetResult();
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
                var assemblyLocation = typeof(ModernHTTPHandler).Assembly.Location;
                var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
                var resourcePath = Path.Combine(assemblyDirectory, "Web.Resources", name);
                
                if (File.Exists(resourcePath))
                {
                    return File.ReadAllBytes(resourcePath);
                }
                else
                {
                    logger.Warn($"Resource file not found: {resourcePath}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to get resource: {name}");
            }

            return new byte[0];
        }

        private async Task<bool> SendIconAsync(RequestEventArgs e, string ext)
        {
            try
            {
                byte[] data = null;
                
                lock (sync)
                {
                    // Check if the icon has been requested already - if so just return that
                    if (iconCache.ContainsKey(ext))
                    {
                        data = iconCache[ext];
                        logger.Debug($"Using cached icon for extension: {ext}");
                    }
                    else
                    {
                        // Icon wasn't cached, generate it
                        if (ext == "folder")
                        {
                            logger.Debug("Attempting to load folder icon");
                            data = GetResource("Images/folder.png");
                            if (data.Length > 0)
                            {
                                iconCache.Add("folder", data);
                                logger.Debug($"Cached folder icon, size: {data.Length} bytes");
                            }
                            else
                            {
                                logger.Warn("Folder icon not found in resources");
                                e.Response.StatusCode = 404;
                                e.IsHandled = true;
                                return false;
                            }
                        }
                        else
                        {
                            // First, check if a static icon file exists for this extension
                            logger.Debug($"Checking for static icon: Images/{ext}.png");
                            data = GetResource($"Images/{ext}.png");
                            
                            if (data.Length > 0)
                            {
                                // Static icon found, cache it
                                iconCache.Add(ext, data);
                                logger.Debug($"Cached static icon for extension: {ext}, size: {data.Length} bytes");
                            }
                            else
                            {
                                logger.Debug($"No static icon found for extension: {ext}, will generate dynamically");
                                // No static icon found, generate one dynamically
                                try
                                {
                                    logger.Debug($"Attempting to generate icon for extension: {ext}");
                                    Icon icon = IconReader.GetFileIcon("file." + ext, IconReader.IconSize.Small, false);
                                    
                                    if (icon == null)
                                    {
                                        logger.Warn($"IconReader.GetFileIcon returned null for extension: {ext}");
                                        e.Response.StatusCode = 404;
                                        e.IsHandled = true;
                                        return false;
                                    }
                                    
                                    using (var mem = new MemoryStream())
                                    {
                                        using (Bitmap bmp = icon.ToBitmap())
                                        {
                                            bmp.MakeTransparent();
                                            bmp.Save(mem, ImageFormat.Png);
                                            data = mem.ToArray();
                                        }
                                    }
                                    
                                    if (data.Length == 0)
                                    {
                                        logger.Warn($"Generated icon data is empty for extension: {ext}");
                                        e.Response.StatusCode = 404;
                                        e.IsHandled = true;
                                        return false;
                                    }
                                    
                                    // Cache the generated icon
                                    iconCache.Add(ext, data);
                                    logger.Debug($"Generated and cached dynamic icon for extension: {ext}, size: {data.Length} bytes");
                                }
                                catch (Exception ex)
                                {
                                    logger.Error(ex, $"Failed to generate icon for extension: {ext}");
                                    e.Response.StatusCode = 404;
                                    e.IsHandled = true;
                                    return false;
                                }
                            }
                        }
                    }
                }
                
                e.Response.ContentType = "image/png";
                await e.Response.Body.WriteAsync(data, 0, data.Length);
                e.IsHandled = true;
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error sending icon");
                e.Response.StatusCode = 500;
                e.IsHandled = true;
                return false;
            }
        }

        private bool SendIcon(RequestEventArgs e, string ext)
        {
            // For backward compatibility, use the async version
            return SendIconAsync(e, ext).GetAwaiter().GetResult();
        }

        private async Task<bool> SendFileAsync(RequestEventArgs e, string path, string url)
        {
            try
            {
                string fileExtension = Path.GetExtension(path);
                if (fileExtension != null && fileExtension.StartsWith("."))
                    fileExtension = fileExtension.Substring(1);

                e.Response.ContentType = GetContentType(fileExtension);

                DateTime modified = File.GetLastWriteTime(path).ToUniversalTime();

                // Only send file if it has not been modified.
                var browserCacheDate = e.Request.Headers["If-Modified-Since"];
                if (!string.IsNullOrEmpty(browserCacheDate))
                {
                    DateTime since = DateTime.Parse(browserCacheDate).ToUniversalTime();

                    // Allow for file systems with subsecond time stamps
                    modified = new DateTime(modified.Year, modified.Month, modified.Day, modified.Hour, modified.Minute,
                                            modified.Second, modified.Kind);
                    if (since >= modified)
                    {
                        e.Response.StatusCode = 304; // Not Modified
                        e.IsHandled = true;
                        return true;
                    }
                }

                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    e.Response.Headers["Last-Modified"] = modified.ToString("R");
                    // Send response
                    var buffer = new byte[fs.Length];
                    int totalBytesRead = 0;
                    int bytesRead;
                    while (totalBytesRead < fs.Length && 
                           (bytesRead = await fs.ReadAsync(buffer, totalBytesRead, (int)fs.Length - totalBytesRead)) > 0)
                    {
                        totalBytesRead += bytesRead;
                    }
                    await e.Response.Body.WriteAsync(buffer, 0, buffer.Length);
                    e.IsHandled = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error sending file");
                e.Response.StatusCode = 500;
                e.IsHandled = true;
                return false;
            }
        }
    }
} 