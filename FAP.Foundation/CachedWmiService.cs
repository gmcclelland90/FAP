using System;
using System.Collections.Generic;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Fap.Foundation
{
    public class CachedWmiService
    {
        private readonly WmiService _wmi;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CachedWmiService>? _logger;

        public CachedWmiService(WmiService wmi, IMemoryCache cache, ILogger<CachedWmiService>? logger = null)
        {
            _wmi = wmi;
            _cache = cache;
            _logger = logger;
        }

        public async Task<T?> QuerySingleWithCacheAsync<T>(string cacheKey, string query, Func<ManagementObject, T> selector, TimeSpan ttl)
        {
            if (_cache.TryGetValue(cacheKey, out T cached)) return cached;
            var result = await _wmi.QuerySingleAsync(query, selector);
            if (result != null)
            {
                _cache.Set(cacheKey, result, ttl);
            }
            return result;
        }

        public async Task<IReadOnlyList<T>> QueryMultipleWithCacheAsync<T>(string cacheKey, string query, Func<ManagementObject, T> selector, TimeSpan ttl)
        {
            if (_cache.TryGetValue(cacheKey, out IReadOnlyList<T> list)) return list;
            var results = await _wmi.QueryMultipleAsync(query, selector);
            _cache.Set(cacheKey, results, ttl);
            return results;
        }
    }
}


