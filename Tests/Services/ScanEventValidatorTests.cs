using System;
using System.Collections.Generic;
using NodaTime;
using PowerAssert;
using Scan_Event_NoSQL.Models;
using Scan_Event_NoSQL.Services;

namespace Tests;

public class ScanEventValidatorTests
{
    [Fact]
    public void ValidateAndFilterInvalidEventType_When_ValidEvents_Should_ReturnAllEvents()
    {
        // Arrange
        var scanEvents = new List<ScanEvent>
        {
            new ScanEvent
            {
                EventId = "valid-event-1",
                ParcelId = 123,
                Type = ScanEventType.Status,
                StatusCode = ScanStatusCode.OrderReceived,
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                RunId = "run-1"
            },
            new ScanEvent
            {
                EventId = "valid-event-2",
                ParcelId = 456,
                Type = ScanEventType.Pickup,
                StatusCode = ScanStatusCode.Dispatched,
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                RunId = "run-1"
            }
        };

        // Act
        var result = ScanEventValidator.ValidateAndFilterInvalidEventType(scanEvents);

        // Assert
        PAssert.IsTrue(() => result.Count == 2);
        PAssert.IsTrue(() => result[0].EventId == "valid-event-1");
        PAssert.IsTrue(() => result[1].EventId == "valid-event-2");
    }

    [Fact]
    public void ValidateAndFilterInvalidEventType_When_InvalidEvents_Should_FilterThem()
    {
        // Arrange
        var scanEvents = new List<ScanEvent>
        {
            new ScanEvent
            {
                EventId = "valid-event",
                ParcelId = 123,
                Type = ScanEventType.Status,
                StatusCode = ScanStatusCode.OrderReceived,
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                RunId = "run-1"
            },
            new ScanEvent
            {
                EventId = "invalid-event",
                ParcelId = 456,
                Type = ScanEventType.Unknown,
                StatusCode = ScanStatusCode.Unknown,
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                RunId = "run-1"
            }
        };

        // Act
        var result = ScanEventValidator.ValidateAndFilterInvalidEventType(scanEvents);

        // Assert
        PAssert.IsTrue(() => result.Count == 1);
        PAssert.IsTrue(() => result[0].EventId == "valid-event");
    }

    [Fact]
    public void ValidateValidEventTypeAndStatusComb_When_ValidCombination_Should_NotThrow()
    {
        // Arrange
        var domainEvents = new List<ScanEvent>
        {
            new ScanEvent
            {
                EventId = "valid-status-event",
                ParcelId = 123,
                Type = ScanEventType.Status,
                StatusCode = ScanStatusCode.OrderReceived,
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                RunId = "run-1"
            }
        };

        // Act & Assert
        // Should not throw an exception
        ScanEventValidator.ValidateValidEventTypeAndStatusComb(domainEvents);
        PAssert.IsTrue(() => true); // Test passes if no exception is thrown
    }

    [Fact]
    public void ValidateValidEventTypeAndStatusComb_When_InvalidCombination_Should_ThrowException()
    {
        // Arrange
        var domainEvents = new List<ScanEvent>
        {
            new ScanEvent
            {
                EventId = "invalid-combo-event",
                ParcelId = 123,
                Type = ScanEventType.Status,
                StatusCode = ScanStatusCode.Dispatched, // Invalid: Dispatched should only be with Pickup type
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                RunId = "run-1"
            }
        };

        // Act & Assert
        PAssert.Throws<ArgumentException>(() => 
            ScanEventValidator.ValidateValidEventTypeAndStatusComb(domainEvents));
    }

    [Fact]
    public void ValidateAndFilterInvalidEventType_When_EmptyList_Should_ReturnEmptyList()
    {
        // Arrange
        var scanEvents = new List<ScanEvent>();

        // Act
        var result = ScanEventValidator.ValidateAndFilterInvalidEventType(scanEvents);

        // Assert
        PAssert.IsTrue(() => result.Count == 0);
    }
}
