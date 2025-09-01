using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Serilog;
using Scan_Event_NoSQL.Models;

namespace Scan_Event_NoSQL.Services;

public class ScanEventApiClientWithCache(HttpClient httpClient, ICacheService cache) : IScanEventApiClient
{
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);
    private readonly ILogger _logger = Log.ForContext<ScanEventApiClientWithCache>();

    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    }.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

    public async Task<ScanEventApiResponse?> GetScanEventsAsync(string? fromEventId = null, int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"scan_events_{fromEventId}_{limit}";

        // Try cache first
        var cached = await cache.GetAsync<ScanEventApiResponse>(cacheKey);
        if (cached != null)
        {
            _logger.Information("Retrieved {Count} scan events from cache for EventId {FromEventId}",
                cached.ScanEvents?.Count ?? 0, fromEventId);
            return cached;
        }

        // Fallback to API
        var result = await CallScanEventsApiAsync(fromEventId, limit, cancellationToken);

        // Cache successful responses
        if (result?.ScanEvents?.Count > 0)
        {
            await cache.SetAsync(cacheKey, result, _cacheExpiry);
        }

        return result;
    }

    private async Task<ScanEventApiResponse?> CallScanEventsApiAsync(string? fromEventId = null, int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"v1/scans/scanevents?FromEventId={fromEventId}&Limit={limit}";
            _logger.Information("Requesting scan events from API: {Url}", url);

            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("API request failed with status code: {StatusCode}, URL: {Url}",
                    response.StatusCode, url);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.Warning("API returned empty content for URL: {Url}", url);
                return null;
            }

            var result = JsonSerializer.Deserialize<ScanEventApiResponse>(content, _jsonOptions);

            if (result?.ScanEvents?.Count > 0)
            {
                _logger.Information("Retrieved {Count} scan events from API starting from EventId {FromEventId}",
                    result.ScanEvents.Count, fromEventId);
            }
            else
            {
                _logger.Information("No scan events returned from API for EventId {FromEventId}", fromEventId);
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.Error(ex, "Failed to deserialize API response for FromEventId: {FromEventId}", fromEventId);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex, "HTTP request failed for FromEventId: {FromEventId}", fromEventId);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.Warning(ex, "API request timed out for FromEventId: {FromEventId}", fromEventId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error occurred while fetching scan events for FromEventId: {FromEventId}",
                fromEventId);
            return null;
        }
    }
}