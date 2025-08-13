using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FAP.Domain.Entities;
using FAP.Domain.Verbs;
using FAP.Network;
using FAP.Shared.Entities;
using FAP.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using System.Collections.Generic;

namespace FAP.Domain.Net
{
    public class ModernHttpClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ModernHttpClient> _logger;
        private readonly INode _callingNode;
        private const int DEFAULT_TIMEOUT = 30000; // 30 seconds

        public ModernHttpClient(INode callingNode, ILogger<ModernHttpClient> logger, HttpClient httpClient)
        {
            _callingNode = callingNode;
            _logger = logger;
            _httpClient = httpClient;
            if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", Model.AppVersion);
            }
        }

        public ModernHttpClient(INode callingNode)
            : this(callingNode, NullLogger<ModernHttpClient>.Instance, new HttpClient { Timeout = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT) })
        {
        }

        public async Task<bool> ExecuteAsync(FAP.Shared.Interfaces.IVerb verb, INode destinationNode)
        {
            return await ExecuteAsync(verb, destinationNode, DEFAULT_TIMEOUT);
        }

        public async Task<bool> ExecuteAsync(FAP.Shared.Interfaces.IVerb verb, string destination)
        {
            return await ExecuteAsync(verb, destination, string.Empty, DEFAULT_TIMEOUT);
        }

        public async Task<bool> ExecuteAsync(FAP.Shared.Interfaces.IVerb verb, string destination, int timeout)
        {
            return await ExecuteAsync(verb, destination, string.Empty, timeout);
        }

        public async Task<bool> ExecuteAsync(NetworkRequest req, INode destination)
        {
            return await ExecuteAsync(req, destination, DEFAULT_TIMEOUT);
        }

        public async Task<bool> ExecuteAsync(NetworkRequest req, INode destination, int timeout)
        {
            destination.LastUpdate = Environment.TickCount;
            var output = new NetworkRequest();
            
            if (!string.IsNullOrEmpty(destination.Secret) && string.IsNullOrEmpty(req.AuthKey))
                req.AuthKey = destination.Secret;
                
            return await DoRequestAsync(destination.Location, req, output, timeout);
        }

        public async Task<bool> ExecuteAsync(FAP.Shared.Interfaces.IVerb verb, string destination, string authKey, int timeout)
        {
            var tempNode = new Node { Location = destination, Secret = authKey };
            return await ExecuteAsync(verb, tempNode, timeout);
        }

        public async Task<bool> ExecuteAsync(FAP.Shared.Interfaces.IVerb verb, INode destination, int timeout)
        {
            try
            {
                NetworkRequest request = verb.CreateRequest();
                var output = new NetworkRequest();
                destination.LastUpdate = Environment.TickCount;

                if (!string.IsNullOrEmpty(destination.Secret) && string.IsNullOrEmpty(request.AuthKey))
                    request.AuthKey = destination.Secret;

                if (!await DoRequestAsync(destination.Location, request, output, timeout))
                    return false;

                if (!verb.ReceiveResponse(output))
                    return false;
                    
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute verb");
                return false;
            }
        }

        public async Task<bool> DoRequestAsync(string url, NetworkRequest input, NetworkRequest result, int timeout)
        {
            if (_callingNode != null)
                input.SourceID = _callingNode.ID;

            try
            {
                var requestUrl = Multiplexor.Encode(url, input.Verb, input.Param);
                string httpMethod = string.IsNullOrEmpty(input.Data) ? "GET" : "POST";
                using var scope = _logger.BeginScope(new Dictionary<string, object>
                {
                    ["Url"] = requestUrl,
                    ["Method"] = httpMethod
                });

                using var request = new HttpRequestMessage();
                request.RequestUri = new Uri(requestUrl);

                // Add FAP headers
                if (!string.IsNullOrEmpty(input.AuthKey))
                    request.Headers.Add("FAP-AUTH", input.AuthKey);
                if (!string.IsNullOrEmpty(input.SourceID))
                    request.Headers.Add("FAP-SOURCE", input.SourceID);
                if (!string.IsNullOrEmpty(input.OverlordID))
                    request.Headers.Add("FAP-OVERLORD", input.OverlordID);

                // Set method and content
                if (string.IsNullOrEmpty(input.Data))
                {
                    request.Method = HttpMethod.Get;
                }
                else
                {
                    request.Method = HttpMethod.Post;
                    request.Content = new StringContent(input.Data, Encoding.UTF8, "application/json");
                }

                // Make the request
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Max(1, timeout)));
                using var response = await _httpClient.SendAsync(request, cts.Token);
                sw.Stop();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("HTTP request failed with status: {StatusCode} for {RequestUrl} in {ElapsedMs} ms", response.StatusCode, requestUrl, sw.ElapsedMilliseconds);
                    return false;
                }

                // Read response data
                if (response.Content.Headers.ContentLength > 0)
                {
                    result.Data = await response.Content.ReadAsStringAsync();
                }

                // Read FAP headers
                if (response.Headers.Contains("FAP-AUTH"))
                    result.AuthKey = response.Headers.GetValues("FAP-AUTH").FirstOrDefault() ?? string.Empty;
                if (response.Headers.Contains("FAP-SOURCE"))
                    result.SourceID = response.Headers.GetValues("FAP-SOURCE").FirstOrDefault() ?? string.Empty;
                if (response.Headers.Contains("FAP-OVERLORD"))
                    result.OverlordID = response.Headers.GetValues("FAP-OVERLORD").FirstOrDefault() ?? string.Empty;

                _logger.LogDebug("HTTP {Method} {RequestUrl} -> {StatusCode} in {ElapsedMs} ms (bytes={Bytes})",
                    request.Method, requestUrl, (int)response.StatusCode, sw.ElapsedMilliseconds,
                    response.Content.Headers.ContentLength ?? 0);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to make request to {Url}", url);
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
} 