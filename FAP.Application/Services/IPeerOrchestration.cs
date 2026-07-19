using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FAP.Domain;
using FAP.Domain.Entities;
using FAP.Domain.Net;
using FAP.Domain.Verbs;
using Microsoft.Extensions.Logging;

namespace FAP.Application.Services
{
    public interface IPeerOrchestration
    {
        Task<IReadOnlyList<CompareNode>> ComparePeersAsync(Model model, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SearchResult>> SearchPeersAsync(Model model, string query, CancellationToken cancellationToken = default);
    }

    public sealed class PeerOrchestration : IPeerOrchestration
    {
        private readonly IFapHttpClientFactory _httpFactory;
        private readonly ILogger<PeerOrchestration> _logger;
        private static readonly Dictionary<string, (CompareNode node, DateTime ts)> CompareCache = new();
        private static readonly TimeSpan CompareCacheTtl = TimeSpan.FromSeconds(60);

        public PeerOrchestration(IFapHttpClientFactory httpFactory, ILogger<PeerOrchestration> logger)
        {
            _httpFactory = httpFactory;
            _logger = logger;
        }

        public async Task<IReadOnlyList<CompareNode>> ComparePeersAsync(Model model, CancellationToken cancellationToken = default)
        {
            var peers = model.Network.Nodes
                .ToList()
                .Where(n => n.NodeType != ClientType.Overlord && n.Online)
                .ToList();

            var results = new System.Collections.Concurrent.ConcurrentBag<CompareNode>();
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount),
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(peers, options, async (peer, ct) =>
            {
                try
                {
                    var peerStart = DateTime.UtcNow;
                    var cacheKey = string.IsNullOrWhiteSpace(peer.Location)
                        ? (peer.Host ?? peer.Nickname ?? Guid.NewGuid().ToString())
                        : peer.Location;
                    lock (CompareCache)
                    {
                        if (CompareCache.TryGetValue(cacheKey, out var cached) && (DateTime.UtcNow - cached.ts) < CompareCacheTtl)
                        {
                            cached.node.Nickname = string.IsNullOrEmpty(peer.Nickname) ? peer.Host ?? string.Empty : peer.Nickname;
                            cached.node.LatencyMs = 0;
                            results.Add(cached.node);
                            return;
                        }
                    }

                    var client = _httpFactory.CreateLegacy(model.LocalNode);
                    var verb = new CompareVerb();
                    var ok = await Task.Run(() => client.Execute(verb, peer, 7000), ct).ConfigureAwait(false);
                    if (!ok)
                    {
                        results.Add(new CompareNode
                        {
                            Nickname = string.IsNullOrEmpty(peer.Nickname) ? peer.Host ?? string.Empty : peer.Nickname,
                            Status = "Error",
                            LatencyMs = (long)(DateTime.UtcNow - peerStart).TotalMilliseconds
                        });
                        return;
                    }

                    var result = verb.Node ?? new CompareNode();
                    if (string.IsNullOrEmpty(result.Nickname))
                        result.Nickname = string.IsNullOrEmpty(peer.Nickname) ? peer.Host ?? string.Empty : peer.Nickname;
                    result.Status = verb.Allowed ? "OK" : "Denied";
                    result.LatencyMs = (long)(DateTime.UtcNow - peerStart).TotalMilliseconds;
                    results.Add(result);
                    lock (CompareCache)
                    {
                        CompareCache[cacheKey] = (result, DateTime.UtcNow);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Compare failed for peer {Peer}", peer?.Nickname ?? peer?.Host ?? "unknown");
                    results.Add(new CompareNode
                    {
                        Nickname = peer?.Nickname ?? peer?.Host ?? string.Empty,
                        Status = "Error"
                    });
                }
            }).ConfigureAwait(false);

            return results.ToList();
        }

        public async Task<IReadOnlyList<SearchResult>> SearchPeersAsync(Model model, string query, CancellationToken cancellationToken = default)
        {
            var peers = model.Network.Nodes
                .ToList()
                .Where(n => n.NodeType != ClientType.Overlord && n.Online)
                .ToList();

            var results = new System.Collections.Concurrent.ConcurrentBag<SearchResult>();
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount),
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(peers, options, async (peer, ct) =>
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(7));
                    var client = _httpFactory.Create(model.LocalNode);
                    var verb = new SearchVerb();
                    verb.SearchString = query;
                    var ok = await client.ExecuteAsync(verb, peer).ConfigureAwait(false);
                    if (!ok || verb.Results == null) return;
                    foreach (var r in verb.Results)
                        results.Add(r);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Search failed for peer {Peer}", peer?.Nickname ?? peer?.Host);
                }
            }).ConfigureAwait(false);

            return results.ToList();
        }
    }
}
