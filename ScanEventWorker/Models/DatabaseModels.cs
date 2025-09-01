using LiteDB;
using NodaTime;

namespace Scan_Event_NoSQL.Models;

public class ParcelScanRecord
{
    [BsonId] public required long ParcelId { get; set; }

    public required string TrackingNumber { get; set; }
    public Instant? PickupDateTimeUtc { get; set; }
    public Instant? DeliveryDateTimeUtc { get; set; }
    public string? LastScanEventId { get; set; }
}

public class ProcessingState
{
    [BsonId] public string Id { get; set; } = "current_state";

    public string? LastProcessedEventId { get; set; }

    public Instant LastProcessedAt { get; set; }
}

public class ScanEventRecord
{
    [BsonId] public required string EventId { get; set; }

    public required long ParcelId { get; set; }
    public required string Type { get; set; }
    public required Instant CreatedDateTimeUtc { get; set; }
    public required string StatusCode { get; set; }
    public required string RunId { get; set; }
    public int? DeviceId { get; set; }
    public string? UserId { get; set; }
    public string? CarrierId { get; set; }
}