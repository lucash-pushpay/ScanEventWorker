using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Scan_Event_NoSQL.Models;
using Scan_Event_NoSQL.Repositories;

namespace Scan_Event_NoSQL.Services;

public class ScanEventProcessingService(
    IParcelScanRepository parcelScanRepository,
    IScanEventRepository scanEventRepository,
    IProcessingStateRepository processingStateRepository,
    ILiteDbTransactionService transactionService)
    : IScanEventProcessingService
{
    private readonly ILogger _logger = Log.ForContext<ScanEventProcessingService>();

    public async Task ProcessScanEventBatchAsync(IEnumerable<ScanEventApiDto> apiEvents)
    {
        var apiEventsList = apiEvents.ToList();

        if (apiEventsList.Count == 0)
        {
            _logger.Information("No events to process");
            return;
        }

        _logger.Information("Processing batch of {Count} scan events", apiEventsList.Count);

        var eventPairs = apiEventsList
            .Select(ApiToDomainMapper.MapToDomain)
            .ToList();

        var validEventPairs = ScanEventValidator.ValidateAndFilterInvalidEventType(eventPairs);

        if (validEventPairs.Count == 0)
        {
            _logger.Warning("No valid events to process after filtering");
            var lastEventId = apiEventsList.Max(e => e.EventId);
            await processingStateRepository.SaveLastProcessedEventIdAsync(lastEventId);
            return;
        }

        // Process valid events in transaction
        await transactionService.ExecuteInTransactionAsync(async () =>
        {
            ScanEventValidator.ValidateValidEventTypeAndStatusComb(validEventPairs);

            var scanEventRecords = DomainToDataMapper.MapToData(validEventPairs);
            await scanEventRepository.SaveScanEventsAsync(scanEventRecords);

            await parcelScanRepository.UpdateParcelsWithScanEventsAsync(validEventPairs);

            var lastProcessedId = apiEventsList.Max(e => e.EventId);
            await processingStateRepository.SaveLastProcessedEventIdAsync(lastProcessedId);

            _logger.Information(
                "Successfully processed {ValidCount} scan events, last processed EventId: {LastEventId}",
                validEventPairs.Count, lastProcessedId);
        });
    }
}