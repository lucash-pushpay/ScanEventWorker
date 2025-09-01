using System;
using System.Threading.Tasks;
using LiteDB;
using NodaTime;
using PowerAssert;
using Scan_Event_NoSQL.Models;
using Scan_Event_NoSQL.Repositories;

namespace Tests;

public class ProcessingStateRepositoryTests : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ProcessingStateRepository _sut;

    public ProcessingStateRepositoryTests()
    {
        _database = new LiteDatabase(":memory:");
        _sut = CreateSut();
    }

    [Fact]
    public async Task GetLastProcessedEventIdAsync_When_NoStateExists_Should_ReturnNull()
    {
        // Arrange
        // No state exists in the database

        // Act
        var result = await _sut.GetLastProcessedEventIdAsync();

        // Assert
        PAssert.IsTrue(() => result == null);
    }

    [Fact]
    public async Task GetLastProcessedEventIdAsync_When_StateExists_Should_ReturnEventId()
    {
        // Arrange
        var expectedEventId = "test-event-123";
        var stateCollection = _database.GetCollection<ProcessingState>("processing_state");
        stateCollection.Insert(new ProcessingState
        {
            Id = "current_state",
            LastProcessedEventId = expectedEventId,
            LastProcessedAt = SystemClock.Instance.GetCurrentInstant()
        });

        // Act
        var result = await _sut.GetLastProcessedEventIdAsync();

        // Assert
        PAssert.IsTrue(() => result == expectedEventId);
    }

    [Fact]
    public async Task SaveLastProcessedEventIdAsync_When_NoExistingState_Should_CreateNewState()
    {
        // Arrange
        var eventId = "new-event-456";

        // Act
        await _sut.SaveLastProcessedEventIdAsync(eventId);

        // Assert
        var stateCollection = _database.GetCollection<ProcessingState>("processing_state");
        var savedState = stateCollection.FindById("current_state");
        
        PAssert.IsTrue(() => savedState != null);
        PAssert.IsTrue(() => savedState!.LastProcessedEventId == eventId);
        PAssert.IsTrue(() => savedState.Id == "current_state");
    }

    [Fact]
    public async Task SaveLastProcessedEventIdAsync_When_ExistingState_Should_UpdateState()
    {
        // Arrange
        var initialEventId = "initial-event";
        var updatedEventId = "updated-event-789";
        
        var stateCollection = _database.GetCollection<ProcessingState>("processing_state");
        stateCollection.Insert(new ProcessingState
        {
            Id = "current_state",
            LastProcessedEventId = initialEventId,
            LastProcessedAt = SystemClock.Instance.GetCurrentInstant()
        });

        // Act
        await _sut.SaveLastProcessedEventIdAsync(updatedEventId);

        // Assert
        var savedState = stateCollection.FindById("current_state");
        
        PAssert.IsTrue(() => savedState != null);
        PAssert.IsTrue(() => savedState!.LastProcessedEventId == updatedEventId);
        PAssert.IsTrue(() => savedState.Id == "current_state");
    }

    [Fact]
    public async Task SaveLastProcessedEventIdAsync_When_NullEventId_Should_SaveNullValue()
    {
        // Arrange
        string? nullEventId = null;

        // Act
        await _sut.SaveLastProcessedEventIdAsync(nullEventId);

        // Assert
        var stateCollection = _database.GetCollection<ProcessingState>("processing_state");
        var savedState = stateCollection.FindById("current_state");
        
        PAssert.IsTrue(() => savedState != null);
        PAssert.IsTrue(() => savedState!.LastProcessedEventId == null);
    }

    private ProcessingStateRepository CreateSut()
    {
        return new ProcessingStateRepository(_database);
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}
