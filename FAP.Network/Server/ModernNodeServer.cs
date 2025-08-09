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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FAP.Network.Entities;

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
                        webBuilder.UseUrls($"http://{address}:{port}");
                        webBuilder.Configure(app =>
                        {
                            app.UseRouting();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGet("/{**path}", HandleRequest);
                                endpoints.MapPost("/{**path}", HandleRequest);
                            });
                        });
                    })
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton(_serviceProvider);
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

                // Check User-Agent to determine if this is a FAP request
                string userAgent = request.Headers["User-Agent"].FirstOrDefault() ?? string.Empty;
                bool isFapRequest = userAgent.StartsWith("FAP");

                // Create request event args with context
                var modernContext = new ModernHttpContext(context);
                var requestArgs = new RequestEventArgs(new ModernHttpRequest(request), new ModernHttpResponse(response), modernContext);

                // Determine request type
                RequestType requestType = isFapRequest ? RequestType.FAP : RequestType.HTTP;

                _logger.LogDebug("Incoming {Method} {Path} (Type={RequestType}) UA={UserAgent}", request.Method, request.Path, requestType, userAgent);

                // Invoke the OnRequest event (async if available, sync as fallback)
                if (OnRequestAsync != null)
                {
                    await OnRequestAsync(this, requestArgs);
                }
                else
                {
                    OnRequest?.Invoke(this, requestArgs);
                }

                if (!requestArgs.IsHandled)
                {
                    _logger.LogWarning("Unhandled request {Method} {Path}", request.Method, request.Path);
                    // Only set status code if response hasn't started yet
                    if (!context.Response.HasStarted)
                    {
                        response.StatusCode = 500;
                        string errorMessage = "Handler error";
                        response.ContentLength = System.Text.Encoding.UTF8.GetByteCount(errorMessage);
                        await response.WriteAsync(errorMessage);
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
                    context.Response.StatusCode = 500;
                    string errorMessage = "Internal server error";
                    context.Response.ContentLength = System.Text.Encoding.UTF8.GetByteCount(errorMessage);
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