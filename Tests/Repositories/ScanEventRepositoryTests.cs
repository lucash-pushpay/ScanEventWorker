using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using NodaTime;
using PowerAssert;
using Scan_Event_NoSQL.Models;
using Scan_Event_NoSQL.Repositories;

namespace Tests;

public class ScanEventRepositoryTests : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ScanEventRepository _sut;

    public ScanEventRepositoryTests()
    {
        _database = new LiteDatabase(":memory:");
        _sut = CreateSut();
    }

    [Fact]
    public async Task SaveScanEventsAsync_When_NewEvents_Should_SaveAllEvents()
    {
        // Arrange
        var scanEventRecords = new List<ScanEventRecord>
        {
            new ScanEventRecord
            {
                EventId = "event-1",
                ParcelId = 123,
                Type = "Status",
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                StatusCode = "OrderReceived",
                RunId = "run-1"
            },
            new ScanEventRecord
            {
                EventId = "event-2",
                ParcelId = 456,
                Type = "Pickup",
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                StatusCode = "Dispatched",
                RunId = "run-1"
            }
        };

        // Act
        await _sut.SaveScanEventsAsync(scanEventRecords);

        // Assert
        var scanEventsCollection = _database.GetCollection<ScanEventRecord>("scan_events");
        var savedEvents = scanEventsCollection.FindAll().ToList();
        
        PAssert.IsTrue(() => savedEvents.Count == 2);
        PAssert.IsTrue(() => savedEvents.Any(e => e.EventId == "event-1"));
        PAssert.IsTrue(() => savedEvents.Any(e => e.EventId == "event-2"));
    }

    [Fact]
    public async Task SaveScanEventsAsync_When_DuplicateEvents_Should_SkipDuplicates()
    {
        // Arrange
        var existingEvent = new ScanEventRecord
        {
            EventId = "existing-event",
            ParcelId = 789,
            Type = "Status",
            CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
            StatusCode = "OrderReceived",
            RunId = "existing-run"
        };
        
        var scanEventsCollection = _database.GetCollection<ScanEventRecord>("scan_events");
        scanEventsCollection.Insert(existingEvent);

        var newScanEventRecords = new List<ScanEventRecord>
        {
            new ScanEventRecord
            {
                EventId = "existing-event", // Duplicate
                ParcelId = 789,
                Type = "Status",
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                StatusCode = "OrderReceived",
                RunId = "new-run"
            },
            new ScanEventRecord
            {
                EventId = "new-event",
                ParcelId = 999,
                Type = "Delivery",
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                StatusCode = "Delivered",
                RunId = "new-run"
            }
        };

        // Act
        await _sut.SaveScanEventsAsync(newScanEventRecords);

        // Assert
        var allEvents = scanEventsCollection.FindAll().ToList();
        
        PAssert.IsTrue(() => allEvents.Count == 2); // Only 1 new event should be added
        PAssert.IsTrue(() => allEvents.Any(e => e.EventId == "existing-event"));
        PAssert.IsTrue(() => allEvents.Any(e => e.EventId == "new-event"));
        
        var existingEventAfter = allEvents.First(e => e.EventId == "existing-event");
        PAssert.IsTrue(() => existingEventAfter.RunId == "existing-run");
    }

    [Fact]
    public async Task SaveScanEventsAsync_When_EmptyList_Should_NotThrow()
    {
        // Arrange
        var emptyScanEventRecords = new List<ScanEventRecord>();

        // Act & Assert
        await _sut.SaveScanEventsAsync(emptyScanEventRecords);
        
        var scanEventsCollection = _database.GetCollection<ScanEventRecord>("scan_events");
        var allEvents = scanEventsCollection.FindAll().ToList();
        PAssert.IsTrue(() => allEvents.Count == 0);
    }

    [Fact]
    public async Task SaveScanEventsAsync_When_AllDuplicateEvents_Should_SaveNone()
    {
        // Arrange
        var existingEvents = new List<ScanEventRecord>
        {
            new ScanEventRecord
            {
                EventId = "event-1",
                ParcelId = 111,
                Type = "Status",
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                StatusCode = "OrderReceived",
                RunId = "run-1"
            },
            new ScanEventRecord
            {
                EventId = "event-2",
                ParcelId = 222,
                Type = "Pickup",
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                StatusCode = "Dispatched",
                RunId = "run-1"
            }
        };
        
        var scanEventsCollection = _database.GetCollection<ScanEventRecord>("scan_events");
        scanEventsCollection.InsertBulk(existingEvents);

        var duplicateScanEventRecords = new List<ScanEventRecord>
        {
            new ScanEventRecord
            {
                EventId = "event-1", // Duplicate
                ParcelId = 111,
                Type = "Status",
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                StatusCode = "InTransit",
                RunId = "run-2"
            },
            new ScanEventRecord
            {
                EventId = "event-2", // Duplicate
                ParcelId = 222,
                Type = "Delivery",
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                StatusCode = "Delivered",
                RunId = "run-2"
            }
        };

        // Act
        await _sut.SaveScanEventsAsync(duplicateScanEventRecords);

        // Assert
        var allEvents = scanEventsCollection.FindAll().ToList();
        
        PAssert.IsTrue(() => allEvents.Count == 2);
        PAssert.IsTrue(() => allEvents.All(e => e.RunId == "run-1"));
    }

    private ScanEventRepository CreateSut()
    {
        return new ScanEventRepository(_database);
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}
