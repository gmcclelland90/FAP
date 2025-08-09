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
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Microsoft.Extensions.Options;
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
                                endpoints.MapGet("/health", async context =>
                                {
                                    context.Response.ContentType = "text/plain";
                                    await context.Response.WriteAsync("OK");
                                });
                                // Consolidated routing
                                endpoints.MapMethods("/Fap.app/{**path}", new[] { "GET", "POST" }, HandleRequest)
                                         .RequireRateLimiting("default");
                                endpoints.MapMethods("/{**path}", new[] { "GET", "POST" }, HandleRequest);
                            });
                        });
                    })
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton(_serviceProvider);
                        services.Configure<FapListenOptions>(_ => { });
                        services.Configure<FapWebOptions>(_ => { });
                        services.AddResponseCompression(o =>
                        {
                            o.EnableForHttps = true;
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
                            options.AddFixedWindowLimiter("default", o =>
                            {
                                o.Window = TimeSpan.FromSeconds(1);
                                o.PermitLimit = 200; // tune for LAN
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

        public void Stop()
        {
            try
            {
                _logger.LogDebug("Stopping modern node server");
                _host?.StopAsync().Wait();
                _host?.Dispose();
                _logger.LogDebug("Modern node server stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping modern node server");
            }
        }

        private async Task HandleRequest(HttpContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;
                var sw = Stopwatch.StartNew();
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
                _logger.LogDebug("Handled {Method} {Path} -> {StatusCode} in {ElapsedMs} ms", request.Method, request.Path, response.StatusCode, sw.ElapsedMilliseconds);
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