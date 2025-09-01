using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NodaTime;
using PowerAssert;
using Scan_Event_NoSQL.Models;
using Scan_Event_NoSQL.Repositories;
using Scan_Event_NoSQL.Services;

namespace Tests;

public class ScanEventProcessingServiceTests
{
    private readonly Mock<IParcelScanRepository> _mockParcelScanRepository;
    private readonly Mock<IScanEventRepository> _mockScanEventRepository;
    private readonly Mock<IProcessingStateRepository> _mockProcessingStateRepository;
    private readonly Mock<ILiteDbTransactionService> _mockTransactionService;
    private readonly ScanEventProcessingService _sut;

    public ScanEventProcessingServiceTests()
    {
        _mockParcelScanRepository = new Mock<IParcelScanRepository>();
        _mockScanEventRepository = new Mock<IScanEventRepository>();
        _mockProcessingStateRepository = new Mock<IProcessingStateRepository>();
        _mockTransactionService = new Mock<ILiteDbTransactionService>();
        _sut = CreateSut();
    }

    [Fact]
    public async Task ProcessScanEventBatchAsync_When_ValidEvents_Should_ProcessSuccessfully()
    {
        // Arrange
        var apiEvents = new List<ScanEventApiDto>
        {
            new ScanEventApiDto
            {
                EventId = "event-1",
                ParcelId = 123,
                Type = "STATUS",
                StatusCode = "ORDER_RECEIVED",
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                User = new UserApiDto { RunId = "run-1", UserId = "user-1" }
            }
        };

        _mockTransactionService.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
            .Returns((Func<Task> action) => action());

        _mockScanEventRepository.Setup(x => x.SaveScanEventsAsync(It.IsAny<IEnumerable<ScanEventRecord>>()))
            .Returns(Task.CompletedTask);

        _mockParcelScanRepository.Setup(x => x.UpdateParcelsWithScanEventsAsync(It.IsAny<List<ScanEvent>>()))
            .Returns(Task.CompletedTask);

        _mockProcessingStateRepository.Setup(x => x.SaveLastProcessedEventIdAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ProcessScanEventBatchAsync(apiEvents);

        // Assert
        _mockTransactionService.Verify(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()), Times.Once);
        _mockScanEventRepository.Verify(x => x.SaveScanEventsAsync(It.IsAny<IEnumerable<ScanEventRecord>>()), Times.Once);
        _mockParcelScanRepository.Verify(x => x.UpdateParcelsWithScanEventsAsync(It.IsAny<List<ScanEvent>>()), Times.Once);
        _mockProcessingStateRepository.Verify(x => x.SaveLastProcessedEventIdAsync("event-1"), Times.Once);
    }

    [Fact]
    public async Task ProcessScanEventBatchAsync_When_EmptyEvents_Should_ReturnEarly()
    {
        // Arrange
        var apiEvents = new List<ScanEventApiDto>();

        // Act
        await _sut.ProcessScanEventBatchAsync(apiEvents);

        // Assert
        _mockTransactionService.Verify(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()), Times.Never);
        _mockScanEventRepository.Verify(x => x.SaveScanEventsAsync(It.IsAny<IEnumerable<ScanEventRecord>>()), Times.Never);
    }

    [Fact]
    public async Task ProcessScanEventBatchAsync_When_AllInvalidEvents_Should_SaveLastEventIdOnly()
    {
        // Arrange
        var apiEvents = new List<ScanEventApiDto>
        {
            new ScanEventApiDto
            {
                EventId = "invalid-event-1",
                ParcelId = 456,
                Type = "UNKNOWN",
                StatusCode = "INVALID",
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                User = new UserApiDto { RunId = "run-2" }
            }
        };

        _mockProcessingStateRepository.Setup(x => x.SaveLastProcessedEventIdAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ProcessScanEventBatchAsync(apiEvents);

        // Assert
        _mockProcessingStateRepository.Verify(x => x.SaveLastProcessedEventIdAsync("invalid-event-1"), Times.Once);
        _mockTransactionService.Verify(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()), Times.Never);
    }

    [Fact]
    public async Task ProcessScanEventBatchAsync_When_MultipleEvents_Should_SaveLastEventId()
    {
        // Arrange
        var apiEvents = new List<ScanEventApiDto>
        {
            new ScanEventApiDto
            {
                EventId = "event-1",
                ParcelId = 123,
                Type = "STATUS",
                StatusCode = "ORDER_RECEIVED",
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                User = new UserApiDto { RunId = "run-1" }
            },
            new ScanEventApiDto
            {
                EventId = "event-2",
                ParcelId = 456,
                Type = "PICKUP",
                StatusCode = "DISPATCHED",
                CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                User = new UserApiDto { RunId = "run-1" }
            }
        };

        _mockTransactionService.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
            .Returns((Func<Task> action) => action());

        _mockScanEventRepository.Setup(x => x.SaveScanEventsAsync(It.IsAny<IEnumerable<ScanEventRecord>>()))
            .Returns(Task.CompletedTask);

        _mockParcelScanRepository.Setup(x => x.UpdateParcelsWithScanEventsAsync(It.IsAny<List<ScanEvent>>()))
            .Returns(Task.CompletedTask);

        _mockProcessingStateRepository.Setup(x => x.SaveLastProcessedEventIdAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ProcessScanEventBatchAsync(apiEvents);

        // Assert
        _mockProcessingStateRepository.Verify(x => x.SaveLastProcessedEventIdAsync("event-2"), Times.Once);
    }

    private ScanEventProcessingService CreateSut()
    {
        return new ScanEventProcessingService(
            _mockParcelScanRepository.Object,
            _mockScanEventRepository.Object,
            _mockProcessingStateRepository.Object,
            _mockTransactionService.Object);
    }
}
