using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FAP.Domain.Entities;
using FAP.Domain.Entities.FileSystem;
using FAP.Domain.Models;
using FAP.Domain.Services;
using FAP.Network.Server;
using Fap.Foundation;
using Microsoft.Extensions.Logging;
using Directory = FAP.Domain.Entities.FileSystem.Directory;
using File = System.IO.File;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;

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
        private readonly ILogger<ModernHTTPHandler> logger;

        // Icon cache - missing from original ModernHTTPHandler
        private readonly Dictionary<string, byte[]> iconCache = new Dictionary<string, byte[]>();
        private readonly Dictionary<string, string> iconEtagCache = new Dictionary<string, string>();
        private readonly object sync = new object();

        public ModernHTTPHandler(ShareInfoService i, Model m, BufferService b, ServerUploadLimiterService u, ILogger<ModernHTTPHandler> logger)
        {
            infoService = i;
            model = m;
            bufferService = b;
            uploadLimiter = u;
            this.logger = logger;
        }

        public async Task<bool> HandleAsync(string path, RequestEventArgs e)
        {
            try
            {
                e.Response.StatusCode = 200;
                string decodedPath = Utility.DecodeURL(path);
                byte[] data = null;
                string etag = string.Empty;

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
                    
                    string? ext = Path.GetExtension(decodedPath);
                    if (!string.IsNullOrEmpty(ext) && ext.StartsWith("."))
                        ext = ext.Substring(1);

                    e.Response.ContentType = GetContentType(ext ?? string.Empty);
                }
                else
                {
                    // Try to resolve the path to a file first
                    string[] possiblePaths;
                    if (infoService.ToLocalPath(decodedPath, out possiblePaths))
                        foreach (string possiblePath in possiblePaths)
                            if (File.Exists(possiblePath))
                                return await SendFileAsync(e, possiblePath, decodedPath);

                    // Build strongly-typed browse page data
                    int freeslots = model.MaxUploads - uploadLimiter.GetActiveTokenCount();
                    string currentPath = decodedPath.EndsWith("/")
                        ? decodedPath.Replace("#", "%23")
                        : (decodedPath + "/").Replace("#", "%23");

                    var pageData = new BrowsePageData
                    {
                        Nickname = model.LocalNode?.Nickname ?? string.Empty,
                        NodeId = model.LocalNode?.ID ?? string.Empty,
                        AppVersion = Model.AppVersion,
                        FreeLimit = Utility.FormatBytes(Model.FREE_FILE_LIMIT),
                        MaxUploadSlots = model.MaxUploads,
                        FreeUploadSlots = freeslots,
                        QueueInfo = freeslots > 0 ? "" : "  Queue length: " + uploadLimiter.GetQueueLength() + ".",
                        SlotColour = freeslots > 0 ? "green" : "red",
                        CurrentPath = currentPath
                    };

                    // Build path breadcrumb segments
                    string[] split = decodedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < split.Length; i++)
                    {
                        var sb = new StringBuilder("/");
                        for (int y = 0; y <= i; y++)
                        {
                            sb.Append(split[y]);
                            sb.Append("/");
                        }
                        pageData.PathSegments.Add(new PathSegment { Name = split[i], Path = sb.ToString() });
                    }

                    // Build file listing
                    long totalSize = 0;
                    List<BrowsingFile> results;
                    if (infoService.GetPath(decodedPath, false, true, out results))
                    {
                        foreach (var browsingFile in results)
                        {
                            var entry = new BrowseFileEntry
                            {
                                Name = browsingFile.Name,
                                Size = browsingFile.Size,
                                SizeText = Utility.FormatBytes(browsingFile.Size),
                                LastModifiedText = browsingFile.LastModified.ToShortDateString(),
                                LastModified = browsingFile.LastModified
                            };

                            if (browsingFile.IsFolder)
                            {
                                entry.Path = Utility.EncodeURL(browsingFile.Name);
                                entry.Icon = "folder";
                                entry.HasIcon = true;
                                entry.IconHtml = $"<img height=\"16px\" width=\"16px\" src=\"{WEB_ICON_PREFIX}folder\" alt=\"icon\" />";
                            }
                            else
                            {
                                string ext = Path.GetExtension(browsingFile.Name);
                                if (ext != null && ext.StartsWith("."))
                                    ext = ext.Substring(1);

                                string name = browsingFile.Name;
                                if (!string.IsNullOrEmpty(name))
                                    name = name.Replace("#", "%23");

                                entry.Path = name;
                                entry.Icon = ext ?? string.Empty;
                                entry.HasIcon = !string.IsNullOrEmpty(ext);
                                entry.IconHtml = !string.IsNullOrEmpty(ext)
                                    ? $"<img height=\"16px\" width=\"16px\" src=\"{WEB_ICON_PREFIX}{ext}\" alt=\"icon\" />"
                                    : string.Empty;
                            }

                            pageData.Files.Add(entry);
                            totalSize += browsingFile.Size;
                        }
                        results.Clear();
                    }

                    pageData.TotalSize = Utility.FormatBytes(totalSize);

                    logger.LogDebug("Rendering browse page for path '{Path}' with {FileCount} entries", decodedPath, pageData.Files.Count);
                    string page = BrowsePageRenderer.Render(pageData);

                    data = Encoding.UTF8.GetBytes(page);
                    e.Response.ContentType = "text/html";
                    e.Response.Headers["Cache-Control"] = "no-store";
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
                logger.LogError(ex, "Error handling HTTP request");
                e.Response.StatusCode = 500;
                e.IsHandled = true;
            }

            return false;
        }

        public bool Handle(string path, RequestEventArgs e)
        {
            // Prefer fire-and-forget async path to avoid blocking threads
            _ = HandleAsync(path, e);
            return true;
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
                var resourcePath = Path.Combine(assemblyDirectory ?? string.Empty, "Web.Resources", name);
                
                if (File.Exists(resourcePath))
                {
                    return File.ReadAllBytes(resourcePath);
                }
                else
                {
                    logger.LogWarning("Resource file not found: {Path}", resourcePath);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get resource: {Name}", name);
            }

            return new byte[0];
        }

        private async Task<bool> SendIconAsync(RequestEventArgs e, string ext)
        {
            try
            {
                byte[] data = null;
                string etag = string.Empty;
                
                lock (sync)
                {
                    // Check if the icon has been requested already - if so just return that
                    if (iconCache.ContainsKey(ext))
                    {
                        data = iconCache[ext];
                        if (iconEtagCache.TryGetValue(ext, out var cachedTag))
                            etag = cachedTag;
                        logger.LogDebug("Using cached icon for extension: {Ext}", ext);
                    }
                    else
                    {
                        // Icon wasn't cached, generate it
                        if (ext == "folder")
                        {
                            logger.LogDebug("Attempting to load folder icon");
                            data = GetResource("Images/folder.png");
                            if (data.Length > 0)
                            {
                                iconCache.Add("folder", data);
                                etag = ComputeEtag("folder", data);
                                iconEtagCache["folder"] = etag;
                                logger.LogDebug("Cached folder icon, size: {Length} bytes", data.Length);
                            }
                            else
                            {
                                logger.LogWarning("Folder icon not found in resources");
                                e.Response.StatusCode = 404;
                                e.IsHandled = true;
                                return false;
                            }
                        }
                        else
                        {
                            // First, check if a static icon file exists for this extension
                            logger.LogDebug("Checking for static icon: Images/{Ext}.png", ext);
                            data = GetResource($"Images/{ext}.png");
                            
                            if (data.Length > 0)
                            {
                                // Static icon found, cache it
                                iconCache.Add(ext, data);
                                etag = ComputeEtag(ext, data);
                                iconEtagCache[ext] = etag;
                                logger.LogDebug("Cached static icon for extension: {Ext}, size: {Length} bytes", ext, data.Length);
                            }
                            else
                            {
                                logger.LogDebug("No static icon found for extension: {Ext}, will generate dynamically", ext);
                                // No static icon found, generate one dynamically
                                try
                                {
                                    logger.LogDebug("Attempting to generate icon for extension: {Ext}", ext);
                                    Icon icon = IconReader.GetFileIcon("file." + ext, IconReader.IconSize.Small, false);
                                    
                                    if (icon == null)
                                    {
                                        logger.LogWarning("IconReader.GetFileIcon returned null for extension: {Ext}", ext);
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
                                        logger.LogWarning("Generated icon data is empty for extension: {Ext}", ext);
                                        e.Response.StatusCode = 404;
                                        e.IsHandled = true;
                                        return false;
                                    }
                                    
                                    // Cache the generated icon
                                    iconCache.Add(ext, data);
                                    etag = ComputeEtag(ext, data);
                                    iconEtagCache[ext] = etag;
                                    logger.LogDebug("Generated and cached dynamic icon for extension: {Ext}, size: {Length} bytes", ext, data.Length);
                                }
                                catch (Exception ex)
                                {
                                    logger.LogError(ex, "Failed to generate icon for extension: {Ext}", ext);
                                    e.Response.StatusCode = 404;
                                    e.IsHandled = true;
                                    return false;
                                }
                            }
                        }
                    }
                }
                // Conditional response: ETag handling
                if (!string.IsNullOrEmpty(etag))
                {
                    var inm = e.Request.Headers["If-None-Match"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(inm) && string.Equals(inm, etag, StringComparison.Ordinal))
                    {
                        e.Response.StatusCode = 304; // Not Modified
                        e.IsHandled = true;
                        return true;
                    }
                    e.Response.Headers["ETag"] = etag;
                }
                // Cache for 30 days (icons rarely change)
                e.Response.Headers["Cache-Control"] = "public,max-age=2592000";
                e.Response.ContentType = "image/png";
                await e.Response.Body.WriteAsync(data, 0, data.Length);
                e.IsHandled = true;
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending icon");
                e.Response.StatusCode = 500;
                e.IsHandled = true;
                return false;
            }
        }

        private static string ComputeEtag(string key, byte[] data)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(data);
            var b64 = Convert.ToBase64String(hash);
            return $"\"{key}-{data.Length}-{b64}\""; // quoted ETag
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
                    e.Response.Headers["Accept-Ranges"] = "bytes";
                    // Lightweight strong ETag using length + last-write time
                    string fileEtag = $"\"{fs.Length}-{modified.Ticks}\"";
                    e.Response.Headers["ETag"] = fileEtag;

                    long totalLength = fs.Length;
                    long start = 0;
                    long end = totalLength - 1;
                    bool isRange = false;

                    var rangeHeader = e.Request.Headers["Range"];
                    var ifRangeHeader = e.Request.Headers["If-Range"];
                    if (!string.IsNullOrEmpty(rangeHeader))
                    {
                        // Expected formats:
                        // bytes=start-end | bytes=start- | bytes=-suffix
                        // Only single-range supported
                        var value = rangeHeader.ToString().Trim();
                        if (value.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
                        {
                            var spec = value.Substring(6).Trim();
                            var parts = spec.Split('-', 2);
                            if (parts.Length == 2)
                            {
                                if (long.TryParse(parts[0], out var parsedStart))
                                {
                                    start = parsedStart;
                                    if (parts[1].Length > 0 && long.TryParse(parts[1], out var parsedEnd))
                                    {
                                        end = parsedEnd;
                                    }
                                    else
                                    {
                                        end = totalLength - 1;
                                    }
                                }
                                else if (parts[0].Length == 0 && long.TryParse(parts[1], out var suffix))
                                {
                                    // bytes=-N
                                    if (suffix <= 0)
                                    {
                                        start = 0;
                                        end = totalLength - 1;
                                    }
                                    else
                                    {
                                        start = Math.Max(0, totalLength - suffix);
                                        end = totalLength - 1;
                                    }
                                }

                                // Validate range
                                if (start < 0 || start >= totalLength)
                                {
                                    // 416 Range Not Satisfiable
                                    e.Response.StatusCode = 416;
                                    e.Response.Headers["Content-Range"] = $"bytes */{totalLength}";
                                    e.IsHandled = true;
                                    return true;
                                }
                                if (end < start || end >= totalLength)
                                {
                                    end = totalLength - 1;
                                }

                                isRange = true;
                            }
                        }
                    }

                    // If-Range: only honor Range when validator matches
                    if (isRange && !string.IsNullOrEmpty(ifRangeHeader))
                    {
                        var ifRange = ifRangeHeader.ToString().Trim();
                        bool validatorMatches = false;
                        if (ifRange.StartsWith("\"") && ifRange.EndsWith("\""))
                        {
                            validatorMatches = string.Equals(ifRange, fileEtag, StringComparison.Ordinal);
                        }
                        else if (DateTime.TryParse(ifRange, out var ifRangeDate))
                        {
                            validatorMatches = modified <= ifRangeDate.ToUniversalTime();
                        }
                        if (!validatorMatches)
                        {
                            isRange = false;
                        }
                    }

                    if (isRange)
                    {
                        long length = end - start + 1;
                        e.Response.StatusCode = 206; // Partial Content
                        e.Response.Headers["Content-Range"] = $"bytes {start}-{end}/{totalLength}";
                        e.Response.Headers["Content-Length"] = length.ToString();

                        // HEAD: headers only
                        if (string.Equals(e.Request.Method, "HEAD", StringComparison.OrdinalIgnoreCase))
                        {
                            e.IsHandled = true;
                            return true;
                        }

                        fs.Position = start;
                        var remaining = length;
                        var buffer = new byte[64 * 1024];
                        while (remaining > 0)
                        {
                            int toRead = (int)Math.Min(buffer.Length, remaining);
                            int bytesRead = await fs.ReadAsync(buffer, 0, toRead);
                            if (bytesRead <= 0) break;
                            await e.Response.Body.WriteAsync(buffer, 0, bytesRead);
                            remaining -= bytesRead;
                        }
                    }
                    else
                    {
                        e.Response.StatusCode = 200;
                        e.Response.Headers["Content-Length"] = totalLength.ToString();
                        // Conditional GET via ETag
                        var inm = e.Request.Headers["If-None-Match"];
                        if (!string.IsNullOrEmpty(inm) && string.Equals(inm.ToString(), fileEtag, StringComparison.Ordinal))
                        {
                            e.Response.StatusCode = 304;
                            e.IsHandled = true;
                            return true;
                        }
                        // HEAD: headers only
                        if (string.Equals(e.Request.Method, "HEAD", StringComparison.OrdinalIgnoreCase))
                        {
                            e.IsHandled = true;
                            return true;
                        }
                        // Stream full file
                        var buffer = new byte[64 * 1024];
                        int bytesRead;
                        while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await e.Response.Body.WriteAsync(buffer, 0, bytesRead);
                        }
                    }

                    e.IsHandled = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending file");
                e.Response.StatusCode = 500;
                e.IsHandled = true;
                return false;
            }
        }
    }
} 