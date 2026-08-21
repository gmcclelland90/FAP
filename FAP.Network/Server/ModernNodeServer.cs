using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FAP.Network.Entities;
using FAP.GuestWeb;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Microsoft.Extensions.Options;
using Fap.Foundation;
using FAP.Shared;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace FAP.Network.Server
{
    public enum RequestType
    {
        FAP,
        HTTP
    }

    public class ModernNodeServer : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ModernNodeServer> _logger;
        private IHost _host;
        private bool _disposed = false;

        public event EventHandler<RequestEventArgs> OnRequest;
        public event Func<object, RequestEventArgs, Task> OnRequestAsync;

        public ModernNodeServer(IServiceProvider serviceProvider, ILogger<ModernNodeServer> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public void Start(IPAddress address, int port)
        {
            try
            {
                _logger.LogDebug("Starting modern node server on {Address}:{Port}", address, port);

                _host = Host.CreateDefaultBuilder()
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseKestrel((context, k) =>
                        {
                            // Listen on provided address/port and enable HTTP/1.1 + HTTP/2 + HTTP/3
                            k.Listen(address, port, lo =>
                            {
                                lo.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                            });

                            // Apply Kestrel limits from configuration if present
                            var limits = context.Configuration.GetSection("Fap:Web:KestrelLimits");
                            var maxConn = limits.GetValue<int?>("MaxConcurrentConnections");
                            if (maxConn.HasValue) k.Limits.MaxConcurrentConnections = maxConn.Value;
                            var maxBody = limits.GetValue<long?>("MaxRequestBodySize");
                            if (maxBody.HasValue) k.Limits.MaxRequestBodySize = maxBody.Value;
                            var keepAlive = limits.GetValue<TimeSpan?>("KeepAliveTimeout");
                            if (keepAlive.HasValue) k.Limits.KeepAliveTimeout = keepAlive.Value;
                            var reqHdrs = limits.GetValue<TimeSpan?>("RequestHeadersTimeout");
                            if (reqHdrs.HasValue) k.Limits.RequestHeadersTimeout = reqHdrs.Value;

                            k.AddServerHeader = false;
                        });
                        webBuilder.Configure(app =>
                        {
                            // Performance middleware (toggle via options)
                            var webOptions = app.ApplicationServices.GetService<IOptions<FapWebOptions>>()?.Value ?? new FapWebOptions();
                            if (webOptions.EnableCompression)
                                app.UseResponseCompression();
                            if (webOptions.EnableCaching)
                                app.UseResponseCaching();
                            if (webOptions.EnableRateLimiting)
                                app.UseRateLimiter();

                            // Serve legacy web resources at /Fap.app.web
                            var staticRoot = Path.Combine(AppContext.BaseDirectory, "Web.Resources");
                            if (Directory.Exists(staticRoot))
                            {
                                app.UseStaticFiles(new StaticFileOptions
                                {
                                    FileProvider = new PhysicalFileProvider(staticRoot),
                                    RequestPath = "/Fap.app.web",
                                    OnPrepareResponse = ctx =>
                                    {
                                        int maxAge = Math.Max(0, (app.ApplicationServices.GetService<IOptions<FapWebOptions>>()?.Value.StaticFilesCacheSeconds) ?? 86400);
                                        ctx.Context.Response.Headers["Cache-Control"] = $"public,max-age={maxAge}";
                                    }
                                });
                            }

                            app.UseRouting();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapRazorPages();
                                endpoints.MapGet("/Fap.api/health", async context =>
                                {
                                    context.Response.ContentType = "text/plain";
                                    await context.Response.WriteAsync("OK");
                                }).RequireRateLimiting("interactive");
                                endpoints.MapGet("/Fap.api/health/details", async context =>
                                {
                                    long ir = System.Threading.Interlocked.Read(ref _interactiveRequests);
                                    long it = System.Threading.Interlocked.Read(ref _interactiveTotalMs);
                                    long dr = System.Threading.Interlocked.Read(ref _downloadsRequests);
                                    long dt = System.Threading.Interlocked.Read(ref _downloadsTotalMs);
                                    long rr = System.Threading.Interlocked.Read(ref _defaultRequests);
                                    long rt = System.Threading.Interlocked.Read(ref _defaultTotalMs);
                                    long i429 = System.Threading.Interlocked.Read(ref _interactive429);
                                    long d429 = System.Threading.Interlocked.Read(ref _downloads429);
                                    long r429 = System.Threading.Interlocked.Read(ref _default429);
                                    var payload = new
                                    {
                                        uptimeMs = _uptime.ElapsedMilliseconds,
                                        activeRequests = _activeRequests,
                                        time = DateTimeOffset.UtcNow,
                                        rateLimit429 = new { interactive = i429, downloads = d429, @default = r429 },
                                        chat = new {
                                            received = FAP.Shared.FapMetrics.Read(ref FAP.Shared.FapMetrics.ChatReceived),
                                            forwarded = FAP.Shared.FapMetrics.Read(ref FAP.Shared.FapMetrics.ChatForwarded),
                                            failures = FAP.Shared.FapMetrics.Read(ref FAP.Shared.FapMetrics.ChatFailures),
                                            clientReceived = FAP.Shared.FapMetrics.Read(ref FAP.Shared.FapMetrics.ClientChatReceived),
                                            conversationSent = FAP.Shared.FapMetrics.Read(ref FAP.Shared.FapMetrics.ConversationSent),
                                            conversationDelivered = FAP.Shared.FapMetrics.Read(ref FAP.Shared.FapMetrics.ConversationDelivered)
                                        },
                                        search = new {
                                            requested = FAP.Shared.FapMetrics.Read(ref FAP.Shared.FapMetrics.SearchRequested),
                                            completed = FAP.Shared.FapMetrics.Read(ref FAP.Shared.FapMetrics.SearchCompleted),
                                            failures = FAP.Shared.FapMetrics.Read(ref FAP.Shared.FapMetrics.SearchFailures)
                                        },
                                        timingsMs = new
                                        {
                                            interactive = new { count = ir, total = it, avg = ir > 0 ? (double)it / ir : 0.0 },
                                            downloads = new { count = dr, total = dt, avg = dr > 0 ? (double)dt / dr : 0.0 },
                                            @default = new { count = rr, total = rt, avg = rr > 0 ? (double)rt / rr : 0.0 }
                                        }
                                    };
                                    context.Response.ContentType = "application/json";
                                    await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(payload));
                                }).RequireRateLimiting("interactive");
                                // Consolidated routing
                                endpoints.MapMethods("/Fap.app/{**path}", new[] { "GET", "POST" }, HandleRequest)
                                         .RequireRateLimiting("interactive");
                                endpoints.MapGet("/Fap.app.web/{**path}", HandleRequest)
                                         .RequireRateLimiting("downloads");
                                endpoints.MapMethods("/{**path}", new[] { "GET", "POST" }, HandleRequest)
                                         .RequireRateLimiting("default");

                                // Optional minimal API: typed COMPARE response endpoint
                                // Typed API (doesn't interfere with legacy /Fap.app/COMPARE)
                                endpoints.MapGet("/Fap.api/compare/v1", async context =>
                                {
                                    try
                                    {
                                        // scope/maxAge (optional, future): var scope = context.Request.Query["scope"].ToString();
                                        var hw = new HardwareInfoService(new WmiService());
                                        var sys = new SystemInfo();
                                        var cpuTask = hw.GetProcessorInfoAsync();
                                        var memTask = hw.GetMemoryInfoAsync();
                                        var gpuTask = hw.GetVideoControllersAsync();
                                        var diskTask = hw.GetDiskInfoAsync();
                                        var nicTask = hw.GetNetworkAdaptersAsync();
                                        var soundTask = hw.GetPrimarySoundDeviceAsync();
                                        await Task.WhenAll(cpuTask, memTask, gpuTask, diskTask, nicTask, soundTask);

                                        var cpu = await cpuTask;
                                        var memBytes = (await memTask).Sum(m => m.Capacity);
                                        var gpus = await gpuTask;
                                        var firstGpu = gpus.FirstOrDefault();
                                        long gpuMem = 0; foreach (var g in gpus) { if (long.TryParse(g.AdapterRAM, out var v)) gpuMem += v; }
                                        var disks = await diskTask;
                                        long dTotal = 0, dFree = 0; int dCount = 0; foreach (var d in disks) { dTotal += d.Size; dFree += d.FreeSpace; dCount++; }
                                        var nics = await nicTask; long link = 0; foreach (var n in nics) link = Math.Max(link, n.Speed);
                                        var sound = await soundTask;

                                        // Basic score: reuse existing calculation indirectly is non-trivial here; set to 0 for typed view
                                        var specs = new CompareSpecsV1(
                                            Cpu: new CpuInfoV1(
                                                Model: cpu?.Name ?? sys.GetCPUType(),
                                                Cores: cpu?.NumberOfCores != 0 ? cpu!.NumberOfCores : sys.GetCPUCores(),
                                                Threads: cpu?.NumberOfLogicalProcessors != 0 ? cpu!.NumberOfLogicalProcessors : sys.GetCPUThreads(),
                                                Bits: sys.GetCPUBits(),
                                                MaxClockMhz: cpu?.MaxClockSpeed != 0 ? cpu!.MaxClockSpeed : sys.GetCPUSpeed()
                                            ),
                                            Memory: new MemoryInfoV1(memBytes != 0 ? memBytes : sys.GetMemorySize()),
                                            Gpu: string.IsNullOrEmpty(firstGpu?.Name) ? null : new GpuInfoV1(firstGpu!.Name, gpuMem, gpus.Count),
                                            Display: new DisplayInfoV1(sys.GetPrimaryDisplayWidth(), sys.GetPrimaryDisplayHeight(), sys.GetTotalDisplayWidth(), sys.GetTotalDisplayHeight()),
                                            Storage: new StorageInfoV1(dTotal != 0 ? dTotal : sys.GetTotalHDDSize(), dFree != 0 ? dFree : sys.GetTotalHDDFree(), dCount != 0 ? dCount : sys.GetHDDCount()),
                                            Network: new NetworkInfoV1(link != 0 ? link : sys.GetNetworkSpeed()),
                                            Audio: string.IsNullOrEmpty(sound) ? null : new AudioInfoV1(sound),
                                            Score: 0
                                        );
                                        var dto = new CompareResponseV1(
                                            Allowed: true,
                                            DenyReason: null,
                                            Nickname: Environment.MachineName,
                                            Location: $"http://{context.Request.Host}",
                                            Specs: specs
                                        );
                                        context.Response.ContentType = "application/json";
                                        await context.Response.WriteAsync(JsonSerializer.Serialize(dto, CompareJsonContext.Default.CompareResponseV1));
                                    }
                                    catch (Exception ex)
                                    {
                                        var logger = context.RequestServices.GetService<Microsoft.Extensions.Logging.ILogger<ModernNodeServer>>();
                                        logger?.LogError(ex, "Error on typed COMPARE endpoint");
                                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                                    }
                                }).RequireRateLimiting("interactive");
                            });
                        });
                    })
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton(_serviceProvider);
                        services.Configure<FapListenOptions>(_ => { });
                        services.Configure<FapWebOptions>(_ => { });
                        services.AddFapGuestWeb();
                        services.AddResponseCompression(o =>
                        {
                            o.EnableForHttps = true;
                            // Focus compression on text types for CPU efficiency on LAN
                            o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
                            {
                                "image/svg+xml",
                                "application/json"
                            });
                            o.Providers.Add<BrotliCompressionProvider>();
                            o.Providers.Add<GzipCompressionProvider>();
                        });
                        services.Configure<BrotliCompressionProviderOptions>(options =>
                        {
                            options.Level = CompressionLevel.Fastest;
                        });
                        services.Configure<GzipCompressionProviderOptions>(options =>
                        {
                            options.Level = CompressionLevel.Fastest;
                        });
                        services.AddResponseCaching();
                        services.AddRateLimiter(options =>
                        {
                            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                            options.OnRejected = (context, token) =>
                            {
                                var path = context.HttpContext?.Request?.Path.Value ?? string.Empty;
                                if (path.StartsWith("/Fap.app/", StringComparison.OrdinalIgnoreCase))
                                {
                                    System.Threading.Interlocked.Increment(ref _interactive429);
                                }
                                else if (path.StartsWith("/Fap.app.web/", StringComparison.OrdinalIgnoreCase))
                                {
                                    System.Threading.Interlocked.Increment(ref _downloads429);
                                }
                                else
                                {
                                    System.Threading.Interlocked.Increment(ref _default429);
                                }
                                return ValueTask.CompletedTask;
                            };
                            // default for general endpoints
                            options.AddFixedWindowLimiter("default", o =>
                            {
                                o.Window = TimeSpan.FromSeconds(1);
                                o.PermitLimit = 200; // tune for LAN
                                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                                o.QueueLimit = 0;
                            });
                            // More generous for downloads
                            options.AddFixedWindowLimiter("downloads", o =>
                            {
                                o.Window = TimeSpan.FromSeconds(1);
                                o.PermitLimit = 400; // allow more parallel GETs
                                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                                o.QueueLimit = 0;
                            });
                            // Tighter for chat/search to protect UI responsiveness
                            options.AddFixedWindowLimiter("interactive", o =>
                            {
                                o.Window = TimeSpan.FromSeconds(1);
                                o.PermitLimit = 100;
                                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                                o.QueueLimit = 0;
                            });
                        });
                    })
                    .Build();

                _host.Start();
                _logger.LogDebug("Modern node server started successfully on {Address}:{Port}", address, port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start modern node server");
                throw;
            }
        }

        public async Task StopAsync()
        {
            try
            {
                _logger.LogDebug("Stopping modern node server");
                if (_host != null)
                {
                    await _host.StopAsync();
                    _host.Dispose();
                }
                _logger.LogDebug("Modern node server stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping modern node server");
            }
        }

        public void Stop()
        {
            StopAsync().GetAwaiter().GetResult();
        }

        private static long _activeRequests = 0;
        private static readonly Stopwatch _uptime = Stopwatch.StartNew();
        // Per-route metrics
        private static long _interactiveRequests = 0;
        private static long _interactiveTotalMs = 0;
        private static long _downloadsRequests = 0;
        private static long _downloadsTotalMs = 0;
        private static long _defaultRequests = 0;
        private static long _defaultTotalMs = 0;
        private static long _interactive429 = 0;
        private static long _downloads429 = 0;
        private static long _default429 = 0;

        private async Task HandleRequest(HttpContext context)
        {
            try
            {
                System.Threading.Interlocked.Increment(ref _activeRequests);
                var request = context.Request;
                var response = context.Response;
                var sw = Stopwatch.StartNew();
                string category = request.Path.HasValue && request.Path.Value!.StartsWith("/Fap.app/") ? "interactive"
                                  : (request.Path.HasValue && request.Path.Value!.StartsWith("/Fap.app.web/") ? "downloads" : "default");
                using var scope = _logger.BeginScope(new Dictionary<string, object>
                {
                    ["RequestId"] = context.TraceIdentifier,
                    ["Method"] = request.Method,
                    ["Path"] = request.Path.ToString()
                });

                var modernContext = new ModernHttpContext(context);
                var modernRequest = new ModernHttpRequest(request);
                var modernResponse = new ModernHttpResponse(response);
                var requestArgs = new RequestEventArgs(modernRequest, modernResponse, modernContext);

                bool isFapPath = request.Path.HasValue && request.Path.Value!.StartsWith("/Fap.app/");
                if (isFapPath)
                {
                    // Ensure body can be read for POST verbs
                    try { request.EnableBuffering(); } catch { }
                    // Decode FAP request centrally and attach it so downstream handlers can reuse
                    try
                    {
                        var decoded = await Multiplexor.DecodeModernAsync(modernRequest);
                        context.Items["FAP.NetworkRequest"] = decoded;
                        _logger.LogTrace("FAP decoded: verb={Verb} dataLen={Len}", decoded.Verb, decoded.Data?.Length ?? 0);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to decode FAP request");
                        if (!context.Response.HasStarted)
                        {
                            response.StatusCode = StatusCodes.Status400BadRequest;
                            response.ContentType = "text/plain";
                            await response.WriteAsync("Bad Request");
                        }
                        return;
                    }

                    // FAP protocol request: forward to subscribers (ListenerService)
                    if (OnRequestAsync != null)
                        await OnRequestAsync(this, requestArgs);
                    else
                        OnRequest?.Invoke(this, requestArgs);

                    if (!requestArgs.IsHandled)
                    {
                        _logger.LogWarning("Unhandled FAP request {Method} {Path}", request.Method, request.Path);
                        if (!context.Response.HasStarted)
                        {
                            response.StatusCode = StatusCodes.Status404NotFound;
                            response.ContentLength = null; // avoid mismatch if a subscriber set Content-Length
                            response.ContentType = "text/plain";
                            await response.WriteAsync("Not Found");
                        }
                    }
                }
                else if (request.Method == HttpMethods.Get && request.Path.HasValue && request.Path.Value!.StartsWith("/Fap.app.web/"))
                {
                    // If static files didn't serve (icon or dynamic), forward to subscribers (ModernHTTPHandler)
                if (OnRequestAsync != null)
                        await OnRequestAsync(this, requestArgs);
                    else
                        OnRequest?.Invoke(this, requestArgs);

                    if (!requestArgs.IsHandled && !context.Response.HasStarted)
                    {
                        response.StatusCode = StatusCodes.Status404NotFound;
                        response.ContentLength = null;
                        response.ContentType = "text/plain";
                        await response.WriteAsync("Not Found");
                    }
                }
                else if (request.Method == HttpMethods.Get)
                {
                    // Let ModernHTTPHandler process legacy dynamic pages (index/template etc.) via the event
                    if (OnRequestAsync != null)
                    await OnRequestAsync(this, requestArgs);
                    else
                        OnRequest?.Invoke(this, requestArgs);

                    if (!requestArgs.IsHandled && !context.Response.HasStarted)
                    {
                        response.StatusCode = StatusCodes.Status404NotFound;
                        response.ContentLength = null;
                        response.ContentType = "text/plain";
                        await response.WriteAsync("Not Found");
                    }
                }
                else
                {
                    // Fallback: hand to subscribers
                    if (OnRequestAsync != null)
                        await OnRequestAsync(this, requestArgs);
                    else
                    OnRequest?.Invoke(this, requestArgs);

                    if (!requestArgs.IsHandled && !context.Response.HasStarted)
                    {
                        response.StatusCode = StatusCodes.Status404NotFound;
                        response.ContentLength = null;
                        response.ContentType = "text/plain";
                        await response.WriteAsync("Not Found");
                    }
                }

                sw.Stop();
                var elapsed = sw.ElapsedMilliseconds;
                switch (category)
                {
                    case "interactive":
                        System.Threading.Interlocked.Increment(ref _interactiveRequests);
                        System.Threading.Interlocked.Add(ref _interactiveTotalMs, elapsed);
                        break;
                    case "downloads":
                        System.Threading.Interlocked.Increment(ref _downloadsRequests);
                        System.Threading.Interlocked.Add(ref _downloadsTotalMs, elapsed);
                        break;
                    default:
                        System.Threading.Interlocked.Increment(ref _defaultRequests);
                        System.Threading.Interlocked.Add(ref _defaultTotalMs, elapsed);
                        break;
                }
                _logger.LogDebug("Handled {Method} {Path} -> {StatusCode} in {ElapsedMs} ms", request.Method, request.Path, response.StatusCode, elapsed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling request");
                // Only set status code if response hasn't started yet
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    const string errorMessage = "Internal server error";
                    // Do not set ContentLength to avoid mismatches if any filters wrote headers
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync(errorMessage);
                }
            }
            finally
            {
                System.Threading.Interlocked.Decrement(ref _activeRequests);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }

    public class ModernHttpRequest
    {
        private readonly HttpRequest _request;

        public ModernHttpRequest(HttpRequest request)
        {
            _request = request;
        }

        public string Method => _request.Method;
        public string Path => _request.Path;
        public IQueryCollection Query => _request.Query;
        public IHeaderDictionary Headers => _request.Headers;
        public Stream Body => _request.Body;
        public string ContentType => _request.ContentType;
        public long ContentLength => _request.ContentLength ?? 0;
    }

    public class ModernHttpResponse
    {
        private readonly HttpResponse _response;

        public ModernHttpResponse(HttpResponse response)
        {
            _response = response;
        }

        public int StatusCode
        {
            get => _response.StatusCode;
            set => _response.StatusCode = value;
        }

        public IHeaderDictionary Headers => _response.Headers;
        public Stream Body => _response.Body;
        public string ContentType
        {
            get => _response.ContentType;
            set => _response.ContentType = value;
        }

        public string Reason => "OK";
        
        public int Status
        {
            get => _response.StatusCode;
            set => _response.StatusCode = value;
        }

        public long? ContentLength
        {
            get => _response.ContentLength;
            set => _response.ContentLength = value;
        }

        public async Task WriteAsync(string content)
        {
            await _response.WriteAsync(content);
        }
    }
} 