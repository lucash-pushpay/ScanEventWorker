using NodaTime;

namespace Scan_Event_NoSQL.Models;

public enum ScanEventType
{
    // For handling unexpected API values
    Unknown = 0,
    Pickup = 1,
    Status = 2,
    Delivery = 3
}

public enum ScanStatusCode
{
    Unknown = 0,

    // STATUS type codes
    OrderReceived = 100,
    Preparing = 101,
    InTransit = 102,
    OutForDelivery = 103,

    // PICKUP type code
    Dispatched = 200,

    // DELIVERY type code
    Delivered = 300
}

public record ScanEvent
{
    public required string EventId { get; set; }
    public required long ParcelId { get; set; }
    public ScanEventType Type { get; set; }
    public ScanStatusCode StatusCode { get; set; }
    public required Instant CreatedDateTimeUtc { get; set; }
    public required string RunId { get; set; }
    public int? DeviceId { get; set; }
    public string? UserId { get; set; }
    public string? CarrierId { get; set; }

    public bool IsUnknownEvent() => Type == ScanEventType.Unknown || StatusCode == ScanStatusCode.Unknown;
}