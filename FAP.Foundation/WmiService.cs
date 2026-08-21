using System;
using System.Collections.Generic;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Fap.Foundation
{
    public class WmiService
    {
        private readonly ILogger<WmiService>? _logger;

        public WmiService(ILogger<WmiService>? logger = null)
        {
            _logger = logger;
        }

        public async Task<T?> QuerySingleAsync<T>(string query, Func<ManagementObject, T> selector)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(query);
                using var collection = await Task.Run(() => searcher.Get());
                foreach (ManagementObject obj in collection)
                {
                    return selector(obj);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "WMI single query failed: {Query}", query);
            }
            return default;
        }

        public async Task<List<T>> QueryMultipleAsync<T>(string query, Func<ManagementObject, T> selector)
        {
            var results = new List<T>();
            try
            {
                using var searcher = new ManagementObjectSearcher(query);
                using var collection = await Task.Run(() => searcher.Get());
                foreach (ManagementObject obj in collection)
                {
                    results.Add(selector(obj));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "WMI multi query failed: {Query}", query);
            }
            return results;
        }
    }
}


