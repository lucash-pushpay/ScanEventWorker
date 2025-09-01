using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiteDB;
using Moq;
using NodaTime;
using PowerAssert;
using Scan_Event_NoSQL.Models;
using Scan_Event_NoSQL.Repositories;

namespace Tests;

public class ParcelScanRepositoryTests : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ParcelScanRepository _sut;

    public ParcelScanRepositoryTests()
    {
        _database = new LiteDatabase(":memory:");
        _sut = CreateSut();
    }

    [Fact]
    public async Task UpdateParcelsWithScanEventsAsync_When_OrderReceivedEventProvided_Should_CreateNewParcelRecord()
    {
        // Arrange
        var scanEvents = new List<ScanEvent>
        {
            new ScanEvent
            {
                EventId = "test-event-1",
                ParcelId = 123,
                Type = ScanEventType.Status,
                StatusCode = ScanStatusCode.OrderReceived,
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                RunId = "test-run-1"
            }
        };

        // Act
        await _sut.UpdateParcelsWithScanEventsAsync(scanEvents);

        // Assert
        var parcelCollection = _database.GetCollection<ParcelScanRecord>("parcel_scans");
        var savedParcel = parcelCollection.FindById(123L);
        
        PAssert.IsTrue(() => savedParcel != null);
        PAssert.IsTrue(() => savedParcel!.ParcelId == 123);
        PAssert.IsTrue(() => savedParcel.TrackingNumber == "TR000123");
        PAssert.IsTrue(() => savedParcel.LastScanEventId == "test-event-1");
    }

    [Fact]
    public async Task UpdateParcelsWithScanEventsAsync_When_PickupEventProvided_Should_UpdateExistingParcel()
    {
        // Arrange
        var existingParcel = new ParcelScanRecord
        {
            ParcelId = 456,
            TrackingNumber = "TR000456",
            LastScanEventId = "initial-event"
        };
        var parcelCollection = _database.GetCollection<ParcelScanRecord>("parcel_scans");
        parcelCollection.Insert(existingParcel);

        var pickupTime = SystemClock.Instance.GetCurrentInstant();
        var scanEvents = new List<ScanEvent>
        {
            new ScanEvent
            {
                EventId = "pickup-event-1",
                ParcelId = 456,
                Type = ScanEventType.Pickup,
                StatusCode = ScanStatusCode.Dispatched,
                CreatedDateTimeUtc = pickupTime,
                RunId = "test-run-2"
            }
        };

        // Act
        await _sut.UpdateParcelsWithScanEventsAsync(scanEvents);

        // Assert
        var updatedParcel = parcelCollection.FindById(456L);
        
        PAssert.IsTrue(() => updatedParcel != null);
        PAssert.IsTrue(() => updatedParcel!.PickupDateTimeUtc != null);
        PAssert.IsTrue(() => updatedParcel.LastScanEventId == "pickup-event-1");
    }

    [Fact]
    public async Task UpdateParcelsWithScanEventsAsync_When_DeliveryEventProvided_Should_UpdateExistingParcel()
    {
        // Arrange
        var existingParcel = new ParcelScanRecord
        {
            ParcelId = 789,
            TrackingNumber = "TR000789",
            LastScanEventId = "initial-event"
        };
        var parcelCollection = _database.GetCollection<ParcelScanRecord>("parcel_scans");
        parcelCollection.Insert(existingParcel);

        var deliveryTime = SystemClock.Instance.GetCurrentInstant();
        var scanEvents = new List<ScanEvent>
        {
            new ScanEvent
            {
                EventId = "delivery-event-1",
                ParcelId = 789,
                Type = ScanEventType.Delivery,
                StatusCode = ScanStatusCode.Delivered,
                CreatedDateTimeUtc = deliveryTime,
                RunId = "test-run-3"
            }
        };

        // Act
        await _sut.UpdateParcelsWithScanEventsAsync(scanEvents);

        // Assert
        var updatedParcel = parcelCollection.FindById(789L);
        
        PAssert.IsTrue(() => updatedParcel != null);
        PAssert.IsTrue(() => updatedParcel!.DeliveryDateTimeUtc != null);
        PAssert.IsTrue(() => updatedParcel.LastScanEventId == "delivery-event-1");
    }

    [Fact]
    public async Task UpdateParcelsWithScanEventsAsync_When_EmptyList_Should_NotThrow()
    {
        // Arrange
        var scanEvents = new List<ScanEvent>();

        // Act & Assert
        await _sut.UpdateParcelsWithScanEventsAsync(scanEvents);
        PAssert.IsTrue(() => true);
    }

    private ParcelScanRepository CreateSut()
    {
        return new ParcelScanRepository(_database);
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}
