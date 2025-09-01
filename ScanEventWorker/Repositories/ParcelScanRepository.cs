using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using Serilog;
using Scan_Event_NoSQL.Models;

namespace Scan_Event_NoSQL.Repositories;

public class ParcelScanRepository : IParcelScanRepository
{
    private readonly ILogger _logger;
    private readonly ILiteCollection<ParcelScanRecord> _collection;

    public ParcelScanRepository(LiteDatabase database)
    {
        _logger = Log.ForContext<ParcelScanRepository>();
        _collection = database.GetCollection<ParcelScanRecord>("parcel_scans");

        _collection.EnsureIndex(x => x.ParcelId);
        // for time-based queries
        _collection.EnsureIndex("DeliveryTracking", x => new { x.DeliveryDateTimeUtc, x.TrackingNumber });
        _collection.EnsureIndex("PickupTracking", x => new { x.PickupDateTimeUtc, x.TrackingNumber });
    }

    public async Task UpdateParcelsWithScanEventsAsync(List<ScanEvent> scanEvents)
    {
        await Task.Run(() =>
        {
            var parcelIds = scanEvents.Select(e => e.ParcelId).Distinct().ToList();
            var existingParcels = _collection
                .Find(x => parcelIds.Contains(x.ParcelId))
                .ToDictionary(x => x.ParcelId); // Create lookup dictionary

            // create new parcel records when ScanEventType.Status && e.StatusCode == ScanStatusCode.OrderReceived
            ProcessOrderReceivedEvents(scanEvents, existingParcels);

            // update existing parcel records with latest pickup/delivery events
            ProcessPickupAndDeliveryEvents(scanEvents, existingParcels);

            PerformBulkUpdate(existingParcels);
        });
    }

    private void ProcessOrderReceivedEvents(List<ScanEvent> scanEvents,
        Dictionary<long, ParcelScanRecord> existingParcels)
    {
        var orderReceivedEvents = scanEvents
            .Where(e => e.Type == ScanEventType.Status && e.StatusCode == ScanStatusCode.OrderReceived)
            .GroupBy(e => e.ParcelId)
            .Select(group =>
                group.OrderByDescending(e => e.CreatedDateTimeUtc).First()) // Get latest order received per parcel
            .ToList();

        foreach (var orderEvent in orderReceivedEvents)
        {
            // Use the cached data instead of individual DB calls
            if (existingParcels.ContainsKey(orderEvent.ParcelId))
            {
                continue;
            }

            // Create new parcel record and add to cache
            var newParcel = CreateParcelRecordForOrderReceived(orderEvent);
            existingParcels[orderEvent.ParcelId] = newParcel;
        }

        if (orderReceivedEvents.Count != 0)
        {
            _logger.Information("Processed {Count} order received events", orderReceivedEvents.Count);
        }
    }

    private void ProcessPickupAndDeliveryEvents(List<ScanEvent> scanEvents,
        Dictionary<long, ParcelScanRecord> existingParcels)
    {
        // Filter to pickup/delivery events, group by parcel, then get only the latest of each type
        var latestPickupOrDeliveryEvents = scanEvents
            .Where(e => e.Type == ScanEventType.Pickup || e.Type == ScanEventType.Delivery)
            .GroupBy(e => e.ParcelId)
            .Select(parcelGroup => new
            {
                ParcelId = parcelGroup.Key,
                LatestPickup = parcelGroup
                    .Where(e => e.Type == ScanEventType.Pickup)
                    .OrderByDescending(e => e.CreatedDateTimeUtc)
                    .FirstOrDefault(),
                LatestDelivery = parcelGroup
                    .Where(e => e.Type == ScanEventType.Delivery)
                    .OrderByDescending(e => e.CreatedDateTimeUtc)
                    .FirstOrDefault()
            })
            .Where(x => x.LatestPickup != null || x.LatestDelivery != null)
            .ToList();

        foreach (var parcelEvents in latestPickupOrDeliveryEvents)
        {
            if (existingParcels.TryGetValue(parcelEvents.ParcelId, out var existingRecord))
            {
                UpdateExistingParcelRecord(existingRecord, parcelEvents.LatestPickup, parcelEvents.LatestDelivery);
            }
        }

        if (latestPickupOrDeliveryEvents.Count != 0)
        {
            _logger.Information("Updated {ParcelCount} parcels with latest pickup/delivery events",
                latestPickupOrDeliveryEvents.Count);
        }
    }

    private ParcelScanRecord CreateParcelRecordForOrderReceived(ScanEvent orderReceivedEvent)
    {
        var trackingNumber = GenerateTrackingNumber(orderReceivedEvent.ParcelId);

        var newRecord = new ParcelScanRecord
        {
            ParcelId = orderReceivedEvent.ParcelId,
            TrackingNumber = trackingNumber,
            LastScanEventId = orderReceivedEvent.EventId
        };
        _logger.Information(
            "Created new parcel record for order received - ParcelId: {ParcelId}, TrackingNumber: {TrackingNumber}",
            orderReceivedEvent.ParcelId, trackingNumber);
        return newRecord;
    }

    private static string GenerateTrackingNumber(long parcelId)
    {
        return $"TR{parcelId:D6}";
    }

    private void UpdateExistingParcelRecord(ParcelScanRecord existingRecord, ScanEvent? latestPickup,
        ScanEvent? latestDelivery)
    {
        bool wasUpdated = false;
        if (latestPickup != null)
        {
            if (UpdatePickupEvent(existingRecord, latestPickup))
            {
                wasUpdated = true;
            }
        }

        if (latestDelivery != null)
        {
            if (UpdateDeliveryEvent(existingRecord, latestDelivery))
            {
                wasUpdated = true;
            }
        }

        if (wasUpdated)
        {
            var mostRecentEvent = GetMostRecentEvent(latestPickup, latestDelivery);
            existingRecord.LastScanEventId = mostRecentEvent.EventId;
        }
    }

    private bool UpdatePickupEvent(ParcelScanRecord parcelRecord, ScanEvent pickupEvent)
    {
        if (parcelRecord.PickupDateTimeUtc.HasValue)
        {
            // If same or older event, skip
            if (pickupEvent.CreatedDateTimeUtc <= parcelRecord.PickupDateTimeUtc.Value)
            {
                return false;
            }

            // If same event ID (duplicate), skip
            if (parcelRecord.LastScanEventId == pickupEvent.EventId)
            {
                _logger.Information("Skipping duplicate pickup event {EventId} for parcel {ParcelId}",
                    pickupEvent.EventId, pickupEvent.ParcelId);
                return false;
            }
        }

        parcelRecord.PickupDateTimeUtc = pickupEvent.CreatedDateTimeUtc;
        _logger.Information("Updated pickup time for parcel {ParcelId}: {PickupTime}",
            pickupEvent.ParcelId, pickupEvent.CreatedDateTimeUtc);
        return true; // Update occurred
    }

    private bool UpdateDeliveryEvent(ParcelScanRecord parcelRecord, ScanEvent deliveryEvent)
    {
        if (parcelRecord.DeliveryDateTimeUtc.HasValue &&
            deliveryEvent.CreatedDateTimeUtc <= parcelRecord.DeliveryDateTimeUtc.Value)
        {
            return false;
        }

        parcelRecord.DeliveryDateTimeUtc = deliveryEvent.CreatedDateTimeUtc;
        _logger.Information("Updated delivery time for parcel {ParcelId}: {DeliveryTime}",
            deliveryEvent.ParcelId, deliveryEvent.CreatedDateTimeUtc);
        return true;
    }

    private static ScanEvent GetMostRecentEvent(ScanEvent? pickupEvent, ScanEvent? deliveryEvent)
    {
        if (pickupEvent == null || deliveryEvent == null)
        {
            return pickupEvent ?? deliveryEvent!;
        }

        return deliveryEvent;
    }

    private void PerformBulkUpdate(Dictionary<long, ParcelScanRecord> existingParcels)
    {
        if (existingParcels.Count == 0) return;

        var newRecords = new List<ParcelScanRecord>();
        var existingRecords = new List<ParcelScanRecord>();

        foreach (var parcel in existingParcels.Values)
        {
            // Check if this is a new record
            var existingInDb = _collection.FindById(parcel.ParcelId);
            if (existingInDb == null)
            {
                newRecords.Add(parcel);
            }
            else
            {
                existingRecords.Add(parcel);
            }
        }

        // Bulk insert new records
        if (newRecords.Count > 0)
        {
            _collection.Insert(newRecords);
            _logger.Information("Bulk inserted {Count} new parcel records", newRecords.Count);
        }

        // Bulk update existing records (not supported by LiteDB)
        // Replace the foreach and use bulk ops eg. updateMany supported by MongoDB
        if (existingRecords.Count > 0)
        {
            foreach (var record in existingRecords)
            {
                _collection.Update(record);
            }

            _logger.Information("Bulk updated {Count} existing parcel records", existingRecords.Count);
        }
    }
}