# FAP HTTP Server Upgrade Plan: Custom HttpServer → Kestrel (Updated for .NET 9)

## Overview

This plan details the migration from the custom `HttpServer` implementation to Microsoft.AspNetCore.Server.Kestrel for .NET 9. The current custom HTTP server is limited in features, performance, and maintainability compared to the battle-tested Kestrel server.

## Current State Analysis

### Custom HttpServer Usage in FAP
- **Location**: `libs/HttpServer/` - legacy HTTP server implementation
- **Usage**: Historically handled both FAP protocol requests and web interface
- **Limitations**: No HTTP/2, limited performance, bespoke stack

### Modern server components already present
- **ASP.NET Core host**: `FAP.Network/Server/ModernNodeServer.cs` (minimal hosting, endpoint routing)
- **Compatibility wrappers**: `ModernHttpRequest`, `ModernHttpResponse`, `ModernHttpContext`, and `RequestEventArgs`
- **Modern HTTP handler**: `FAP.Domain/Handlers/ModernHTTPHandler.cs` (serves `/Fap.app.web/*`, icons and template rendering via existing `TemplateEngine`)
- **Multiplexor**: `FAP.Network/Multiplexor.cs` contains `DecodeModernAsync` supporting ASP.NET Core request decoding

### Legacy references status
- Project references to `..\libs\HttpServer\HttpServer.csproj` have been removed from:
  - `FAP.Application/FAP.Application.csproj`
  - `FAP.Domain/FAP.Domain.csproj`
  - `FAP.Network/FAP.Network.csproj`
  - Solution entry and configs in `Fap.sln`
- Usings of `HttpServer.*` removed in modern code paths (handlers and `FAP.Network/Multiplexor.cs`)
- Physical folder `libs/HttpServer/` is now unused and can be deleted

### Current HTTP Server Architecture
```csharp
// Current HttpServer usage
public class NodeServer
{
    private HttpServer server;
    
    public void Start()
    {
        server = new HttpServer();
        server.RequestReceived += OnRequestReceived;
        server.Start(port);
    }
    
    private void OnRequestReceived(HttpRequest request, HttpResponse response)
    {
        // Custom request handling logic
        if (request.Path.StartsWith("/Fap.app/"))
        {
            // Handle FAP protocol requests
        }
        else if (request.Path.StartsWith("/Fap.app.web/"))
        {
            // Handle web interface requests
        }
    }
}
```

## Target State

### Kestrel Benefits
- ✅ **High Performance**: 2-3x faster than custom implementation
- ✅ **HTTP/2 Support**: Modern protocol support
- ✅ **Battle-Tested**: Used by ASP.NET Core
- ✅ **Built-in Features**: Compression, HTTPS, WebSockets
- ✅ **Configuration**: JSON-based configuration
- ✅ **Middleware**: Extensible pipeline

### Target Architecture
```csharp
// Target Kestrel usage with endpoint routing on .NET 9
public static class FapWebHost
{
    public static IHost Build(IPAddress address, int port)
    {
        var builder = WebApplication.CreateBuilder();

        // Configure Kestrel
        builder.WebHost.UseKestrel(k =>
        {
            k.Listen(address, port, lo =>
            {
                lo.Protocols = HttpProtocols.Http1AndHttp2AndHttp3; // enable HTTP/1.1 + HTTP/2 + HTTP/3
            });
            k.AddServerHeader = false;
        });

        // Compression/cache/static files
        builder.Services.AddResponseCompression(o =>
        {
            o.EnableForHttps = true;
            o.Providers.Add<BrotliCompressionProvider>();
            o.Providers.Add<GzipCompressionProvider>();
        });
        builder.Services.AddResponseCaching();

        var app = builder.Build();

        app.UseResponseCompression();
        app.UseResponseCaching();

        // Static files (serve legacy Web.Resources for compatibility)
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(AppContext.BaseDirectory, "Web.Resources")),
            RequestPath = "/Fap.app.web"
        });

        // Endpoint routing: split protocol vs web
        app.MapMethods("/Fap.app/{**path}", new[] { "GET", "POST" }, FapEndpoints.HandleFapAsync);
        app.MapMethods("/Fap.app.web/{**path}", new[] { "GET" }, WebEndpoints.HandleWebAsync);

        return app;
    }
}
```

## Migration Strategy

### Phase 0: Decommission legacy HttpServer (Week 0)

1. Remove project references to `..\libs\HttpServer\HttpServer.csproj` from `FAP.Application`, `FAP.Domain`, `FAP.Network`, and `Fap.sln`.
2. Replace `using HttpServer.*` with modern equivalents:
   - Request/response/context → `ModernHttpRequest`/`ModernHttpResponse`/`ModernHttpContext`
   - Response writing → `ModernResponseWriter` or direct `HttpResponse`
   - Headers → `HttpResponse.Headers`/`HttpRequest.Headers`
3. Update code paths to use `Multiplexor.DecodeModernAsync` (not legacy `Decode`).
4. Keep `HTTPHandler` functionality via `ModernHTTPHandler` (already present) and delete the legacy `HTTPHandler` when usages are migrated.

### Phase 1: Foundation Setup (Week 1)

#### 1.1 Add ASP.NET Core Packages
Note: Not required. We rely on the shared framework (`Microsoft.AspNetCore.App`) in .NET 9; no extra packages needed for Kestrel, StaticFiles, Compression, Caching, or RateLimiting.

#### 1.2 Create FAP Protocol Middleware
```csharp
// FapProtocolMiddleware.cs
public class FapProtocolMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FapProtocolMiddleware> _logger;
    
    public FapProtocolMiddleware(RequestDelegate next, ILogger<FapProtocolMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        
        if (path?.StartsWith("/Fap.app/") == true)
        {
            await HandleFapRequest(context);
        }
        else
        {
            await _next(context);
        }
    }
    
    private async Task HandleFapRequest(HttpContext context)
    {
        var verb = ExtractVerb(context.Request.Path);
        var param = ExtractParam(context.Request.Query);
        
        // Handle FAP protocol requests
        var response = await ProcessFapVerb(verb, param, context);
        
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
```

#### 1.3 Create Web Interface Middleware
```csharp
// WebInterfaceMiddleware.cs
public class WebInterfaceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebInterfaceMiddleware> _logger;
    
    public WebInterfaceMiddleware(RequestDelegate next, ILogger<WebInterfaceMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        
        if (path?.StartsWith("/Fap.app.web/") == true)
        {
            await HandleWebInterface(context);
        }
        else
        {
            await _next(context);
        }
    }
    
    private async Task HandleWebInterface(HttpContext context)
    {
        var relativePath = context.Request.Path.Value?.Replace("/Fap.app.web/", "");
        
        if (string.IsNullOrEmpty(relativePath) || relativePath == "/")
        {
            // Serve main page
            await ServeMainPage(context);
        }
        else if (relativePath.StartsWith("icon/"))
        {
            // Serve file icons
            await ServeFileIcon(context, relativePath);
        }
        else
        {
            // Serve static files
            await ServeStaticFile(context, relativePath);
        }
    }
}
```

### Phase 2: Protocol Migration (Week 2)

#### 2.1 Migrate FAP Protocol Handling
```csharp
// FapProtocolHandler.cs
public class FapProtocolHandler
{
    private readonly ILogger<FapProtocolHandler> _logger;
    private readonly FAPServerHandler _fapHandler;
    private readonly FAPClientHandler _clientHandler;
    
    public FapProtocolHandler(ILogger<FapProtocolHandler> logger, 
                            FAPServerHandler fapHandler, 
                            FAPClientHandler clientHandler)
    {
        _logger = logger;
        _fapHandler = fapHandler;
        _clientHandler = clientHandler;
    }
    
    public async Task<NetworkRequest> ProcessRequest(string verb, string param, HttpContext context)
    {
        var request = new NetworkRequest
        {
            Verb = verb,
            Param = param,
            SourceID = context.Request.Headers["FAP-SourceID"].ToString(),
            OverlordID = context.Request.Headers["FAP-OverlordID"].ToString(),
            AuthKey = context.Request.Headers["FAP-AuthKey"].ToString()
        };
        
        // Route to appropriate handler
        if (IsOverlordRequest(verb))
        {
            return await _fapHandler.ProcessRequest(request);
        }
        else
        {
            return await _clientHandler.ProcessRequest(request);
        }
    }
}
```

#### 2.2 Update Multiplexor for Kestrel
```csharp
// KestrelMultiplexor.cs
public class KestrelMultiplexor
{
    public static NetworkRequest DecodeRequest(HttpContext context)
    {
        var verb = ExtractVerb(context.Request.Path);
        var param = context.Request.Query["p"].ToString();
        
        return new NetworkRequest
        {
            Verb = verb,
            Param = param,
            SourceID = context.Request.Headers["FAP-SourceID"].ToString(),
            OverlordID = context.Request.Headers["FAP-OverlordID"].ToString(),
            AuthKey = context.Request.Headers["FAP-AuthKey"].ToString()
        };
    }
    
    public static async Task EncodeResponse(HttpContext context, NetworkRequest response)
    {
        context.Response.Headers["FAP-SourceID"] = response.SourceID;
        context.Response.Headers["FAP-OverlordID"] = response.OverlordID;
        context.Response.Headers["FAP-AuthKey"] = response.AuthKey;
        
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
```

### Phase 3: Web Interface Compatibility (Week 3)

Goal: keep the existing web UI functional while deferring a full template migration until the MAUI Blazor UI lands.

1. Serve static files from `Web.Resources` via `UseStaticFiles` at `/Fap.app.web` with caching headers.
2. Keep `TemplateEngine` and `ModernHTTPHandler` for the main page and directory listing. Do not build a new template engine.
3. Implement icon endpoints using `ModernHTTPHandler` or map to `/Fap.app.web/icon/{ext}` and generate/cached icons as today.
4. Optional: add a lightweight SPA placeholder page that can later host the MAUI Blazor Web target.

### Phase 4: Configuration and Optimization (Week 4)

#### 4.1 Kestrel Configuration
```csharp
// Program.cs
var builder = WebApplication.CreateBuilder();

builder.WebHost.ConfigureKestrel(options =>
{
    // Configure for high performance
    options.Limits.MaxConcurrentConnections = 100;
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    options.AddServerHeader = false; // hide server header
    options.ConfigureEndpointDefaults(lo =>
    {
        lo.Protocols = HttpProtocols.Http1AndHttp2AndHttp3; // enable HTTP/3
    });

    // Listen on FAP ports
    options.ListenAnyIP(30); // Client port
    options.ListenAnyIP(40); // Overlord port
});

// Add compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

var app = builder.Build();

// Configure middleware pipeline
app.UseResponseCompression();
app.UseResponseCaching();
app.UseMiddleware<FapProtocolMiddleware>();
app.UseMiddleware<WebInterfaceMiddleware>();
app.UseStaticFiles();
```

#### 4.2 Performance Optimizations
```csharp
// Performance optimizations for Kestrel
public class FapWebHost
{
    public void ConfigurePerformance(WebApplicationBuilder builder)
    {
        // Enable HTTP/2
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ConfigureEndpointDefaults(lo =>
            {
                lo.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
            });
        });
        
        // Add caching
        builder.Services.AddResponseCaching();
        
        // Add compression
        builder.Services.AddResponseCompression();

        // Optional: basic rate limiting for protection
        builder.Services.AddRateLimiter(_ => _.AddFixedWindowLimiter("default", o =>
        {
            o.Window = TimeSpan.FromSeconds(1);
            o.PermitLimit = 200; // tune for LAN
            o.QueueLimit = 0;
        }));
    }
}
```

#### 4.3 Configuration via appsettings
```json
{
  "Fap": {
    "ClientPort": 30,
    "OverlordPort": 40
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:30"
      }
    },
    "AddServerHeader": false
  }
}
```

## Migration Checklist

### Foundation
- [x] Add ASP.NET Core packages (N/A in .NET 9 shared framework)
- [ ] Create FapProtocolMiddleware (optional; using endpoint handlers/ModernHTTPHandler for now)
- [ ] Create WebInterfaceMiddleware (optional; using endpoint handlers/ModernHTTPHandler for now)
- [x] Update NodeServer to use Kestrel
- [x] Configure Kestrel for FAP ports

### Protocol Migration
- [ ] Migrate FAP protocol handling
- [x] Update Multiplexor for Kestrel
- [ ] Migrate request/response encoding
- [ ] Update FAP verb processing
- [ ] Test protocol compatibility

### Web Interface Compatibility
 - [x] Serve static files from `Web.Resources` via `UseStaticFiles`
 - [x] Keep `TemplateEngine` and `ModernHTTPHandler` for directory pages
 - [x] Map `/Fap.app.web/icon/{ext}` and cache/generate icons (via ModernHTTPHandler)
 - [x] Add caching headers for static content
 - [x] Add ETag for icons and long-lived cache (30 days)
 - [x] Stream file responses to reduce memory usage
 - [x] Validate web interface functionality

### Configuration
- [x] Configure Kestrel performance settings (HTTP/1.1/2/3)
- [x] Add response compression
- [x] Add response caching
- [x] Add rate limiting policy (fixed window)
- [x] Per-route policies: interactive (`/Fap.app/*`), downloads (`/Fap.app.web/*`), default
- [x] Disable server header
- [ ] Test performance improvements

### Cleanup
- [x] Remove custom HttpServer projects and references
- [x] Remove `using HttpServer.*` and legacy wrappers
- [x] Remove `libs/HttpServer/` folder
- [x] Remove legacy uploaders: `HTTPFileUploader`, `FAPFileUploader`
- [ ] Update project dependencies (as needed)
- [ ] Test all HTTP functionality
- [ ] Performance testing

## What’s next

- Protocol correctness and tests
  - [ ] Exercise CONNECT/BROWSE/SEARCH/DOWNLOAD verbs end-to-end (client ↔ server) on LAN
  - [ ] Ensure no double-read of request bodies (central decode already in place)
  - [ ] Confirm responses consistently written via `ModernResponseWriter` where applicable

- File download path performance
  - [ ] Enable range requests for large file downloads handled in `ModernHTTPHandler` (use `SendFileAsync` or range-aware streaming)
  - [ ] Consider mapping direct downloads to static-file pipeline where feasible to leverage kernel sendfile

- Observability
  - [ ] Track per-route 429 counts and request timings; expose in `/health/details`

- Performance validation
  - [ ] Throughput and latency measurements for typical LAN concurrency
  - [ ] Tune rate limiter permit limits based on observed concurrency

## Risk Assessment

### High Risk
- **Protocol Compatibility**: FAP protocol behavior may change
- **Performance Impact**: Initial migration may affect performance
- **File Serving**: Static file serving behavior changes

### Medium Risk
- **Request Routing**: Different routing mechanism
- **Error Handling**: Different error response format
- **Configuration**: Different configuration approach

### Low Risk
- **Package Dependencies**: Standard ASP.NET Core packages
- **API Compatibility**: Well-documented APIs

## Benefits

### Performance
- **2-3x Faster**: Kestrel is significantly faster than custom server
- **HTTP/2 Support**: Modern protocol with better performance
- **Compression**: Built-in response compression
- **Connection Pooling**: Better connection management

### Features
- **Built-in Security**: HTTPS support, security headers
- **Middleware Pipeline**: Extensible request processing
- **Configuration**: JSON-based configuration
- **Monitoring**: Built-in metrics and monitoring

### Maintenance
- **Battle-Tested**: Used by millions of applications
- **Better Support**: Microsoft-backed
- **Future-Proof**: Aligned with .NET roadmap
- **Documentation**: Extensive documentation and examples

## Testing Strategy

### Unit Tests
- [ ] Test FapProtocolMiddleware (or endpoint handlers)
- [ ] Test WebInterfaceCompatibility (ModernHTTPHandler)
- [ ] Test Multiplexor.DecodeModernAsync
- [ ] Test icon generation/cache

### Integration Tests
- [ ] Test FAP protocol requests (/Fap.app/*)
- [ ] Test web interface requests (/Fap.app.web/*)
- [ ] Test file uploads/downloads
- [ ] Test static file serving with caching

### Performance Tests
- [ ] Measure request throughput
- [ ] Compare with custom HttpServer
- [ ] Test concurrent connections
- [ ] Test memory usage

### Compatibility Tests
- [ ] Test with existing FAP clients
- [ ] Test with existing web browsers
- [ ] Test protocol compatibility (headers/verbs)
- [ ] Test web interface compatibility

## Decision: Defer template engine replacement

- Rationale: UI migration to MAUI Blazor (see `docs/UI-Upgrade-Plan.md`) will supersede the legacy web template. Investing in a new template engine now would be throwaway work.
- Action: Keep existing `TemplateEngine` and `ModernHTTPHandler` for web browsing pages until the MAUI Blazor web target is ready. Revisit removal as part of UI migration.
