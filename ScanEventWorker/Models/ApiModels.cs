using System.Collections.Generic;
using System.Text.Json.Serialization;
using NodaTime;

namespace Scan_Event_NoSQL.Models;

public record ScanEventApiResponse(
    [property: JsonPropertyName("ScanEvents")]
    List<ScanEventApiDto> ScanEvents
)
{
    public ScanEventApiResponse() : this([])
    {
    }
}

public record ScanEventApiDto
{
    [JsonPropertyName("EventId")] public required string EventId { get; init; }

    [JsonPropertyName("ParcelId")] public required long ParcelId { get; init; }

    [JsonPropertyName("Type")] public required string Type { get; init; }

    [JsonPropertyName("StatusCode")] public required string StatusCode { get; init; }

    [JsonPropertyName("CreatedDateTimeUtc")]
    public required Instant CreatedDateTimeUtc { get; init; }

    [JsonPropertyName("Device")] public DeviceApiDto? Device { get; init; }

    [JsonPropertyName("User")] public required UserApiDto User { get; init; }
}

public record DeviceApiDto
{
    [JsonPropertyName("DeviceTransactionId")]
    public int DeviceTransactionId { get; init; }

    [JsonPropertyName("DeviceId")] public int? DeviceId { get; init; }
}

public record UserApiDto
{
    [JsonPropertyName("UserId")] public string? UserId { get; init; }

    [JsonPropertyName("RunId")] public required string RunId { get; init; }

    [JsonPropertyName("CarrierId")] public string? CarrierId { get; init; }
}