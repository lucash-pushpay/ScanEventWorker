using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using Scan_Event_NoSQL.Models;

namespace Scan_Event_NoSQL.Services;

public static class ScanEventValidator
{
    private static readonly ILogger Logger = Log.ForContext(typeof(ScanEventValidator));

    public static List<ScanEvent> ValidateAndFilterInvalidEventType(List<ScanEvent> scanEvents)
    {
        var validEvents = new List<ScanEvent>();

        foreach (var scanEvent in scanEvents)
        {
            if (scanEvent.IsUnknownEvent())
            {
                Logger.Warning("Skipping unknown event - EventId: {EventId}, Type: {Type}, StatusCode: {StatusCode}",
                    scanEvent.EventId, scanEvent.Type, scanEvent.StatusCode);
            }
            else
            {
                validEvents.Add(scanEvent);
            }
        }

        return validEvents;
    }

    public static void ValidateValidEventTypeAndStatusComb(List<ScanEvent> domainEvents)
    {
        foreach (var domainEvent in domainEvents)
        {
            if (!IsValidCombination(domainEvent.Type, domainEvent.StatusCode))
            {
                var errorMessage = GetValidationErrorMessage(domainEvent.Type, domainEvent.StatusCode);
                throw new ArgumentException($"Invalid event {domainEvent.EventId}: {errorMessage}");
            }
        }
    }

    private static bool IsValidCombination(ScanEventType type, ScanStatusCode statusCode)
    {
        if (type == ScanEventType.Unknown || statusCode == ScanStatusCode.Unknown)
            return false;

        return ValidCombinations.ContainsKey(type) &&
               ValidCombinations[type].Contains(statusCode);
    }

    private static string GetValidationErrorMessage(ScanEventType type, ScanStatusCode statusCode)
    {
        if (type == ScanEventType.Unknown)
            return $"Unknown event type: {type}";

        if (statusCode == ScanStatusCode.Unknown)
            return $"Unknown status code: {statusCode}";

        if (ValidCombinations.TryGetValue(type, out var value) && value.Contains(statusCode))
        {
            return string.Empty;
        }

        var validStatusCodes = ValidCombinations.TryGetValue(type, out var combination)
            ? string.Join(", ", combination)
            : "none";
        return
            $"Invalid combination: Type '{type}' cannot have StatusCode '{statusCode}'. Valid status codes for {type} are: {validStatusCodes}";
    }

    private static readonly Dictionary<ScanEventType, HashSet<ScanStatusCode>> ValidCombinations = new()
    {
        {
            ScanEventType.Status, new HashSet<ScanStatusCode>
            {
                ScanStatusCode.OrderReceived,
                ScanStatusCode.Preparing,
                ScanStatusCode.InTransit,
                ScanStatusCode.OutForDelivery
            }
        },
        {
            ScanEventType.Pickup, new HashSet<ScanStatusCode>
            {
                ScanStatusCode.Dispatched
            }
        },
        {
            ScanEventType.Delivery, new HashSet<ScanStatusCode>
            {
                ScanStatusCode.Delivered
            }
        }
    };
}