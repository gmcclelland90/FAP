# HTTP Integration

## Overview

FAP uses HTTP/1.1 as its transport layer, essentially "hitching a ride" on HTTP infrastructure. This clever design allows FAP to leverage existing HTTP infrastructure while adding custom application-layer functionality through the FAP protocol. The system uses a single HTTP server that handles both FAP protocol requests and standard web interface requests, distinguished by URL paths.

### Current implementation (ASP.NET Core)
- Hosting: ASP.NET Core Kestrel
- Routing: `/Fap.app/VERB` (FAP), `/Fap.app.web/...` (web UI), `/Fap.api/...` (typed API)
- Decoding: FAP requests are decoded once in the ASP.NET Core pipeline and forwarded to handlers
- Client: `ModernHttpClient` (HttpClient) with per-request timeouts
- JSON: System.Text.Json

## HTTP Server Architecture

### ModernNodeServer (ASP.NET Core Kestrel)
Kestrel serves FAP protocol, web UI, and typed API on the same port. Requests are routed by URL path; FAP requests are decoded centrally and forwarded to handlers. Middleware includes response compression, response caching, and rate limiting.

### Request Type Detection
Path-based routing only:
- `/Fap.app/` → FAP protocol
- `/Fap.app.web/` → web UI
- `/Fap.api/` → typed API

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
Standard HTTP requests serve the web interface via ASP.NET Core static files and the modern handler:

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
User-Agent: FAP Client
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
In the current system, header extraction and verb decoding happen centrally in the ASP.NET Core pipeline; the decoded `NetworkRequest` is attached to the request context for downstream handlers.

## HTTP Client Implementation

### ModernHttpClient (HttpClient)
Client operations use `HttpClient` with per-request timeouts. FAP headers are attached and requests are encoded via `Multiplexor.Encode(url, verb, param)`.

## Web Interface Integration

### ModernHTTPHandler
The modern HTTP handler provides dynamic content for browsing, implements range requests and conditional headers (ETag/If-None-Match/If-Range), and cooperates with ASP.NET Core static files middleware for `/Fap.app.web/*`.

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
Handled by ASP.NET Core static files; override with `StaticFileOptions` if needed.

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
Connection settings are configured through Kestrel in `appsettings.json`.

### Keep-Alive Support
- **Connection Reuse**: Keep HTTP connections alive for multiple requests
- **Timeout Configuration**: Appropriate timeout values for different operations
- **Resource Management**: Efficient connection pooling

### Compression
Response compression (Brotli/Gzip) is enabled via middleware.

## Security Considerations

### LAN-Only Design
- **Network Isolation**: Designed for trusted LAN environments
- **No HTTPS**: Relies on network-level security
- **Simple Authentication**: Basic secret-based authentication

### Input Validation
Validate paths in handlers and return appropriate status codes. Dynamic pages set `Cache-Control: no-store`; file responses support range and conditional headers.

## Configuration
Server listen address/port, Kestrel limits, compression, caching, and rate limiting are configured via `appsettings.json` (e.g., `Fap:Listen`, `Fap:Web`).

## Monitoring and Debugging

### HTTP Logging
Use `ILogger` for structured logs. Health details and counters are exposed at `/Fap.api/health/details`.

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