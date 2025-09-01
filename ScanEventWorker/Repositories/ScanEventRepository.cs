using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using Serilog;
using Scan_Event_NoSQL.Models;

namespace Scan_Event_NoSQL.Repositories;

public class ScanEventRepository : IScanEventRepository
{
    private readonly ILogger _logger;
    private readonly ILiteCollection<ScanEventRecord> _scanEventsCollection;

    public ScanEventRepository(LiteDatabase database)
    {
        _logger = Log.ForContext<ScanEventRepository>();
        _scanEventsCollection = database.GetCollection<ScanEventRecord>("scan_events");

        _scanEventsCollection.EnsureIndex(x => x.EventId);
        _scanEventsCollection.EnsureIndex(x => x.ParcelId);
        _scanEventsCollection.EnsureIndex(x => x.CreatedDateTimeUtc);
    }

    public async Task SaveScanEventsAsync(IEnumerable<ScanEventRecord> scanEventRecords)
    {
        await Task.Run(() =>
        {
            var recordList = scanEventRecords.ToList();

            var eventIds = recordList.Select(r => r.EventId).ToList();

            // Duplicate event handling
            var existingEventIds = _scanEventsCollection
                .Find(x => eventIds.Contains(x.EventId))
                .Select(x => x.EventId)
                .ToHashSet();
            var newRecords = recordList.Where(r => !existingEventIds.Contains(r.EventId)).ToList();

            if (newRecords.Count > 0)
            {
                _scanEventsCollection.InsertBulk(newRecords);
                _logger.Information("Bulk saved {Count} new scan event records (skipped {Duplicates} duplicates)",
                    newRecords.Count, recordList.Count - newRecords.Count);
            }
            else
            {
                _logger.Information("No new records to save - all {Count} were duplicates", recordList.Count);
            }
        });
    }
}