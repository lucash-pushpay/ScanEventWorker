using System;
using System.Collections.Generic;
using System.Linq;
using NodaTime;
using Scan_Event_NoSQL.Models;

namespace Scan_Event_NoSQL.Services;

public static class ApiToDomainMapper
{
    public static ScanEvent MapToDomain(ScanEventApiDto apiDto)
    {
        return new ScanEvent
        {
            EventId = apiDto.EventId,
            ParcelId = apiDto.ParcelId,
            Type = MapScanEventType(apiDto.Type),
            StatusCode = MapScanStatusCode(apiDto.StatusCode, apiDto.Type),
            CreatedDateTimeUtc = apiDto.CreatedDateTimeUtc,
            RunId = apiDto.User?.RunId ?? throw new InvalidOperationException("RunId is required"),
            DeviceId = apiDto.Device?.DeviceId,
            UserId = apiDto.User?.UserId ?? string.Empty,
            CarrierId = apiDto.User?.CarrierId ?? string.Empty
        };
    }

    private static ScanEventType MapScanEventType(string apiType)
    {
        return apiType?.ToUpperInvariant() switch
        {
            "PICKUP" => ScanEventType.Pickup,
            "STATUS" => ScanEventType.Status,
            "DELIVERY" => ScanEventType.Delivery,
            _ => ScanEventType.Unknown
        };
    }

    private static ScanStatusCode MapScanStatusCode(string apiStatusCode, string apiEventType)
    {
        var statusUpper = apiStatusCode?.ToUpperInvariant();
        var typeUpper = apiEventType?.ToUpperInvariant();

        return (typeUpper, statusUpper) switch
        {
            // STATUS type codes
            ("STATUS", "ORDER_RECEIVED") => ScanStatusCode.OrderReceived,
            ("STATUS", "PREPARING") => ScanStatusCode.Preparing,
            ("STATUS", "IN_TRANSIT") => ScanStatusCode.InTransit,
            ("STATUS", "OUT_FOR_DELIVERY") => ScanStatusCode.OutForDelivery,

            // PICKUP type code
            ("PICKUP", "DISPATCHED") => ScanStatusCode.Dispatched,

            // DELIVERY type code
            ("DELIVERY", "DELIVERED") => ScanStatusCode.Delivered,

            // Fallback for direct status code matching
            (_, "ORDER_RECEIVED") => ScanStatusCode.OrderReceived,
            (_, "PREPARING") => ScanStatusCode.Preparing,
            (_, "IN_TRANSIT") => ScanStatusCode.InTransit,
            (_, "OUT_FOR_DELIVERY") => ScanStatusCode.OutForDelivery,
            (_, "DISPATCHED") => ScanStatusCode.Dispatched,
            (_, "DELIVERED") => ScanStatusCode.Delivered,

            _ => ScanStatusCode.Unknown
        };
    }
}

public static class DomainToDataMapper
{
    public static List<ScanEventRecord> MapToData(IEnumerable<ScanEvent> scanEvents)
    {
        return scanEvents.Select(scanEvent => new ScanEventRecord
        {
            EventId = scanEvent.EventId,
            ParcelId = scanEvent.ParcelId,
            Type = scanEvent.Type.ToString().ToUpperInvariant(),
            StatusCode = MapStatusCodeToString(scanEvent.StatusCode),
            CreatedDateTimeUtc = scanEvent.CreatedDateTimeUtc,
            RunId = scanEvent.RunId,
            DeviceId = scanEvent.DeviceId,
            UserId = scanEvent.UserId,
            CarrierId = scanEvent.CarrierId,
        }).ToList();
    }

    private static string MapStatusCodeToString(ScanStatusCode statusCode)
    {
        return statusCode switch
        {
            ScanStatusCode.OrderReceived => "ORDER_RECEIVED",
            ScanStatusCode.Preparing => "PREPARING",
            ScanStatusCode.InTransit => "IN_TRANSIT",
            ScanStatusCode.OutForDelivery => "OUT_FOR_DELIVERY",
            ScanStatusCode.Dispatched => "DISPATCHED",
            ScanStatusCode.Delivered => "DELIVERED",
            _ => "UNKNOWN"
        };
    }
}