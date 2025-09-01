using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NodaTime;
using Serilog;
using Scan_Event_NoSQL.Repositories;

namespace Scan_Event_NoSQL.Services;

public class ScanEventWorkerService(
    IScanEventApiClient apiClient,
    IProcessingStateRepository processingStateRepository,
    IScanEventProcessingService scanEventProcessingService,
    IConfiguration configuration)
    : BackgroundService
{
    private readonly ILogger _logger = Log.ForContext<ScanEventWorkerService>();
    private const string BatchSize = "Worker:BatchSize";
    private const string PollingIntervalSeconds = "Worker:PollingIntervalSeconds";
    private const string MaxRetryAttempts = "Worker:MaxRetryAttempts";

    private readonly int _batchSize = configuration.GetValue<int>(BatchSize, 100);

    private readonly TimeSpan _pollInterval =
        TimeSpan.FromSeconds(configuration.GetValue<int>(PollingIntervalSeconds, 30));

    private readonly int _maxRetries = configuration.GetValue<int>(MaxRetryAttempts, 3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information(
            "Scan Event Worker Service starting with batch size: {BatchSize}, poll interval: {PollInterval}s",
            _batchSize, _pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScanEventsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error in worker service execution");

                // Add exponential backoff on errors to avoid hammering the API
                var errorDelay = TimeSpan.FromSeconds(Math.Min(60, _pollInterval.TotalSeconds * 2));
                try
                {
                    await Task.Delay(errorDelay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
        }

        _logger.Information("Scan Event Worker Service stopped");
    }

    private async Task ProcessScanEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var lastProcessedEventId = await processingStateRepository.GetLastProcessedEventIdAsync();

            _logger.Information("Fetching scan events from EventId: {FromEventId}", lastProcessedEventId);

            // Implement retry mechanism for API calls
            var response = await ExecuteWithRetryAsync(async () =>
                    await apiClient.GetScanEventsAsync(lastProcessedEventId, _batchSize, cancellationToken),
                cancellationToken);

            if (response?.ScanEvents == null || !response.ScanEvents.Any())
            {
                _logger.Information("No new scan events found");
                return;
            }

            _logger.Information("Retrieved {Count} scan events from API", response.ScanEvents.Count);

            await scanEventProcessingService.ProcessScanEventBatchAsync(response.ScanEvents);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing scan events");
            throw;
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var attempt = 1;
        while (attempt <= _maxRetries)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < _maxRetries && IsRetriableException(ex))
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // Exponential backoff
                _logger.Warning("Attempt {Attempt}/{MaxRetries} failed: {Error}. Retrying in {Delay}s...",
                    attempt, _maxRetries, ex.Message, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
                attempt++;
            }
        }

        // Final attempt - let any exception bubble up
        return await operation();
    }

    private static bool IsRetriableException(Exception ex)
    {
        return ex is HttpRequestException ||
               ex is TaskCanceledException ||
               ex is TimeoutException ||
               (ex is InvalidOperationException && ex.Message.Contains("timeout"));
    }

    // Health check method
    public async Task<WorkerHealthStatus> GetHealthStatusAsync()
    {
        try
        {
            var lastProcessedEventId = await processingStateRepository.GetLastProcessedEventIdAsync();
            return new WorkerHealthStatus
            {
                IsHealthy = true,
                LastProcessedEventId = lastProcessedEventId,
                LastCheckTime = SystemClock.Instance.GetCurrentInstant(),
                BatchSize = _batchSize,
                PollIntervalSeconds = (int)_pollInterval.TotalSeconds
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking worker health status");
            return new WorkerHealthStatus
            {
                IsHealthy = false,
                LastCheckTime = SystemClock.Instance.GetCurrentInstant(),
                ErrorMessage = ex.Message
            };
        }
    }
}

public class WorkerHealthStatus
{
    public bool IsHealthy { get; set; }
    public string? LastProcessedEventId { get; set; }
    public Instant LastCheckTime { get; set; }
    public int BatchSize { get; set; }
    public int PollIntervalSeconds { get; set; }
    public string? ErrorMessage { get; set; }
}