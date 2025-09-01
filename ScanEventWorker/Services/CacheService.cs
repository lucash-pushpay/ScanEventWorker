using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Scan_Event_NoSQL.Services;

public class MemoryCacheService(IMemoryCache memoryCache, ILogger<MemoryCacheService> logger)
    : ICacheService
{
    public Task<T?> GetAsync<T>(string key)
    {
        var result = memoryCache.TryGetValue(key, out var value);

        if (result && value is T typedValue)
        {
            logger.LogDebug("Cache hit for key: {Key}", key);
            return Task.FromResult((T?)typedValue);
        }

        logger.LogDebug("Cache miss for key: {Key}", key);
        return Task.FromResult(default(T));
    }

    public Task SetAsync<T>(string key, T value, TimeSpan expiry)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry,
            Priority = CacheItemPriority.Normal
        };

        memoryCache.Set(key, value, options);
        logger.LogDebug("Cached item with key: {Key}, expiry: {Expiry}", key, expiry);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        memoryCache.Remove(key);
        logger.LogDebug("Removed cache item with key: {Key}", key);
        return Task.CompletedTask;
    }
}