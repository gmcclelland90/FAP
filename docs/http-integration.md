# HTTP Integration

## Overview

FAP uses HTTP/1.1 as its transport layer, essentially "hitching a ride" on HTTP infrastructure. This clever design allows FAP to leverage existing HTTP infrastructure while adding custom application-layer functionality through the FAP protocol. The system uses a single HTTP server that handles both FAP protocol requests and standard web interface requests, distinguished by URL paths.

## HTTP Server Architecture

### NodeServer
The `NodeServer` class provides a unified HTTP server implementation that handles both FAP protocol requests and standard HTTP requests on the same port. This "HTTP hitchhiking" approach allows FAP to leverage existing HTTP infrastructure while maintaining its custom protocol functionality.

```csharp
public class NodeServer
{
    private HttpListener listener;
    public event Request OnRequest;

    public void Start(IPAddress a, int port)
    {
        listener = HttpListener.Create(a, port);
        listener.RequestReceived += listener_RequestReceived;
        listener.Start(1000);
    }

    private void listener_RequestReceived(object sender, RequestEventArgs e)
    {
        e.IsHandled = true;
        e.Response.Reason = string.Empty;
        
        // Determine request type based on User-Agent
        string userAgent = string.Empty;
        IHeader uahead = e.Request.Headers
            .Where(h => string.Equals("User-Agent", h.Name, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
        if (null != uahead)
            userAgent = uahead.HeaderValue;

        // Route to appropriate handler
        if (userAgent.StartsWith("FAP"))
        {
            if (OnRequest(RequestType.FAP, e))
                return;
        }
        if (OnRequest(RequestType.HTTP, e))
            return;
            
        e.Response.Reason = "Handler error";
        e.Response.Status = HttpStatusCode.InternalServerError;
    }
}
```

### Request Type Detection
The server distinguishes between FAP protocol requests and standard HTTP requests based on URL paths and User-Agent headers:

```csharp
public enum RequestType
{
    FAP,    // FAP protocol requests (/Fap.app/VERB)
    HTTP    // Standard HTTP requests (web interface /Fap.app.web/)
}
```

## URL Routing

### Unified Server Design
FAP uses a single HTTP server that handles both protocol and web interface requests, distinguished by URL paths. This "HTTP hitchhiking" approach simplifies network configuration and resource usage.

### FAP Protocol URLs
FAP protocol requests use the `/Fap.app/` URL prefix:

```
http://host:port/Fap.app/VERB?p=base64param
```

Examples:
- `http://192.168.1.100:30/Fap.app/CONNECT`
- `http://192.168.1.100:30/Fap.app/BROWSE?p=L2ZvbGRlcg==`
- `http://192.168.1.100:30/Fap.app/CHAT`

### Web Interface URLs
Standard HTTP requests serve the web interface:

```
http://host:port/Fap.app.web/
```

Examples:
- `http://192.168.1.100:30/Fap.app.web/` (main page)
- `http://192.168.1.100:30/Fap.app.web/css/fap.css` (stylesheets)
- `http://192.168.1.100:30/Fap.app.web/js/jquery-1.5.2.min.js` (scripts)

## HTTP Headers

### Standard Headers
FAP uses standard HTTP headers for basic communication:

```http
GET /Fap.app/CONNECT HTTP/1.1
Host: 192.168.1.100:8080
User-Agent: FAP Beat 7.5ish
Content-Type: application/json
Content-Length: 1024
```

### FAP-Specific Headers
Custom headers carry FAP protocol metadata:

```http
FAP-AUTH: abc123def456
FAP-SOURCE: node123
FAP-OVERLORD: overlord456
```

### Header Processing
```csharp
public static NetworkRequest Decode(IRequest r)
{
    var req = new NetworkRequest();
    
    // Extract FAP headers
    var headers = r.Headers as HeaderCollection;
    if (null != headers)
    {
        foreach (IHeader h in headers)
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
```

## HTTP Client Implementation

### Client Class
The `Client` class handles HTTP client communication:

```csharp
public class Client
{
    public bool DoRequest(string url, NetworkRequest input, out NetworkRequest result, int timeout)
    {
        result = new NetworkRequest();

        try
        {
            var req = (HttpWebRequest)WebRequest.Create(
                Multiplexor.Encode(url, input.Verb, input.Param));
            req.Timeout = timeout;

            // Add standard headers
            req.UserAgent = Model.AppVersion;
            
            // Add FAP headers
            if (!string.IsNullOrEmpty(input.AuthKey))
                req.Headers.Add("FAP-AUTH", input.AuthKey);
            if (!string.IsNullOrEmpty(input.SourceID))
                req.Headers.Add("FAP-SOURCE", input.SourceID);
            if (!string.IsNullOrEmpty(input.OverlordID))
                req.Headers.Add("FAP-OVERLORD", input.OverlordID);

            // Handle POST vs GET
            if (string.IsNullOrEmpty(input.Data))
            {
                req.Method = "GET";
                req.ContentLength = 0;
            }
            else
            {
                req.ContentType = "application/json";
                req.Method = "POST";
                byte[] bytes = Encoding.UTF8.GetBytes(input.Data);
                req.ContentLength = bytes.Length;
                Stream os = req.GetRequestStream();
                os.Write(bytes, 0, bytes.Length);
                os.Flush();
            }

            // Get response
            var resp = (HttpWebResponse)req.GetResponse();
            if (resp.ContentLength > 0)
            {
                using (Stream s = resp.GetResponseStream())
                {
                    using (var sr = new StreamReader(s, Encoding.UTF8))
                    {
                        result.Data = sr.ReadToEnd().Trim();
                    }
                }
            }

            // Extract response headers
            foreach (string header in resp.Headers.AllKeys)
            {
                switch (header)
                {
                    case "FAP-AUTH":
                        result.AuthKey = resp.Headers[header];
                        break;
                    case "FAP-SOURCE":
                        result.SourceID = resp.Headers[header];
                        break;
                    case "FAP-OVERLORD":
                        result.OverlordID = resp.Headers[header];
                        break;
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

## Web Interface Integration

### HTTPHandler
The `HTTPHandler` class serves the web interface for file browsing:

```csharp
public class HTTPHandler
{
    private const string WEB_PREFIX = "/Fap.app.web/";
    private const string WEB_ICON_PREFIX = "/Fap.app.web/icon/";

    public bool Handle(string req, RequestEventArgs e)
    {
        e.Response.Status = HttpStatusCode.OK;
        string path = Utility.DecodeURL(e.Request.Uri.AbsolutePath);
        byte[] data = null;

        // Handle icon requests
        if (path.StartsWith(WEB_ICON_PREFIX))
        {
            string ext = path.Substring(path.LastIndexOf("/") + 1);
            return SendIcon(e, ext);
        }

        // Handle file downloads
        if (infoService.ToLocalPath(path, out possiblePaths))
        {
            foreach (string possiblePath in possiblePaths)
            {
                if (File.Exists(possiblePath))
                    return SendFile(e, possiblePath, path);
            }
        }

        // Handle directory browsing
        List<BrowsingFile> results;
        if (infoService.GetPath(path, false, true, out results))
        {
            // Generate HTML page with file listing
            return SendDirectoryListing(e, path, results);
        }

        return false;
    }
}
```

### Template Engine
The web interface uses a template engine for dynamic content:

```html
<!DOCTYPE html>
<html>
<head>
    <title>$model.LocalNode.Nickname$'s Web share</title>
    <link rel="stylesheet" href="/Fap.app.web/css/fap.css" />
</head>
<body>
    <h1>$model.LocalNode.Nickname$'s Shares</h1>
    <p>Files under $freelimit$ are not queued.</p>
    
    <table id="files">
        <thead>
            <tr>
                <th>Name</th>
                <th>Size</th>
                <th>Modified</th>
            </tr>
        </thead>
        <tbody>
            <!-- Dynamic file listing -->
        </tbody>
    </table>
</body>
</html>
```

## Content Types

### MIME Type Mapping
```csharp
private void AddDefaultMimeTypes()
{
    contentTypes.Add("html", new ContentTypeHeader("text/html"));
    contentTypes.Add("css", new ContentTypeHeader("text/css"));
    contentTypes.Add("js", new ContentTypeHeader("application/javascript"));
    contentTypes.Add("png", new ContentTypeHeader("image/png"));
    contentTypes.Add("jpg", new ContentTypeHeader("image/jpeg"));
    contentTypes.Add("ico", new ContentTypeHeader("image/x-icon"));
    contentTypes.Add("json", new ContentTypeHeader("application/json"));
}
```

### File Type Detection
```csharp
private ContentTypeHeader GetContentType(string path)
{
    string ext = Path.GetExtension(path).ToLower();
    if (ext.StartsWith("."))
        ext = ext.Substring(1);
        
    ContentTypeHeader contentType;
    if (contentTypes.TryGetValue(ext, out contentType))
        return contentType;
        
    return new ContentTypeHeader("application/octet-stream");
}
```

## Error Handling

### HTTP Status Codes
FAP uses standard HTTP status codes:

- **200 OK**: Request successful
- **400 Bad Request**: Malformed request
- **401 Unauthorized**: Authentication failed
- **404 Not Found**: Resource not found
- **500 Internal Server Error**: Server error

### Error Response Format
```json
{
    "Error": "Error message",
    "Code": "ERROR_CODE",
    "Details": "Additional error details"
}
```

## Performance Optimization

### Connection Management
```csharp
// Configure HTTP connection settings
ServicePointManager.Expect100Continue = false;
ServicePointManager.DefaultConnectionLimit = 100;
```

### Keep-Alive Support
- **Connection Reuse**: Keep HTTP connections alive for multiple requests
- **Timeout Configuration**: Appropriate timeout values for different operations
- **Resource Management**: Efficient connection pooling

### Compression
```csharp
// Enable gzip compression for large responses
req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
```

## Security Considerations

### LAN-Only Design
- **Network Isolation**: Designed for trusted LAN environments
- **No HTTPS**: Relies on network-level security
- **Simple Authentication**: Basic secret-based authentication

### Input Validation
```csharp
// Validate URL paths
if (!path.StartsWith("/Fap.app/") && !path.StartsWith("/Fap.app.web/"))
{
    e.Response.Status = HttpStatusCode.NotFound;
    return false;
}

// Sanitize file paths
if (path.Contains(".."))
{
    e.Response.Status = HttpStatusCode.BadRequest;
    return false;
}
```

## Configuration

### Server Settings
```csharp
// HTTP server configuration
listener.Start(1000);  // Backlog size

// Client configuration
req.Timeout = 30000;   // 30 second timeout
req.UserAgent = "FAP Beat 7.5ish";
```

### Port Management
```csharp
// Dynamic port allocation
bool trybind = true;
int port = inport;
do
{
    try
    {
        listener.Start(IPAddress.Parse(model.LocalNode.Host), port);
        trybind = false;
    }
    catch
    {
        port++;
        if (inport + 100 < port)
            throw new Exception("Could not bind listener");
    }
} while (trybind);
```

## Monitoring and Debugging

### HTTP Logging
```csharp
// Log HTTP requests
logger.Debug("HTTP {0} {1}", e.Request.Method, e.Request.Uri.AbsolutePath);

// Log response times
var stopwatch = Stopwatch.StartNew();
// ... handle request ...
stopwatch.Stop();
logger.Debug("Request completed in {0}ms", stopwatch.ElapsedMilliseconds);
```

### Performance Metrics
- **Request Rate**: Requests per second
- **Response Times**: Average response time
- **Error Rates**: Failed requests percentage
- **Bandwidth Usage**: Data transferred

## Best Practices

### HTTP Hitchhiking Design
1. **URL Separation**: Clear distinction between protocol and web interface paths
2. **Single Server**: Unified HTTP server reduces resource usage
3. **Standard Compliance**: Follow HTTP/1.1 specifications
4. **Error Handling**: Proper HTTP status codes and error responses

### HTTP Integration
1. **Request Routing**: Efficient URL-based request routing
2. **Performance**: Efficient connection management and resource usage
3. **Security**: Input validation and proper error handling
4. **Port Management**: Dynamic port allocation with fallback

### Web Interface
1. **User Experience**: Clean, responsive web interface
2. **Accessibility**: Support for different browsers and devices
3. **Performance**: Optimized for file browsing and downloads
4. **Security**: Safe file serving and path validation

### Troubleshooting
1. **Connection Issues**: Check network connectivity and firewall settings
2. **Performance Problems**: Monitor HTTP request/response times
3. **Web Interface Issues**: Verify template engine and file serving
4. **Protocol Errors**: Check FAP header formatting and content types 