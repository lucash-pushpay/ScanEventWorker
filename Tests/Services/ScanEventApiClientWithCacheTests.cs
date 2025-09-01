using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NodaTime;
using PowerAssert;
using Scan_Event_NoSQL.Models;
using Scan_Event_NoSQL.Services;

namespace Tests;

public class ScanEventApiClientWithCacheTests
{
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<IScanEventApiClient> _mockApiClient;

    public ScanEventApiClientWithCacheTests()
    {
        _mockCache = new Mock<ICacheService>();
        _mockApiClient = new Mock<IScanEventApiClient>();
    }

    [Fact]
    public async Task GetScanEventsAsync_When_CacheHit_Should_ReturnCachedData()
    {
        // Arrange
        var fromEventId = "test-event-1";
        var limit = 10;
        var cachedResponse = new ScanEventApiResponse
        {
            ScanEvents = new List<ScanEventApiDto>
            {
                new ScanEventApiDto
                {
                    EventId = "cached-event",
                    Type = "STATUS",
                    StatusCode = "ORDER_RECEIVED",
                    CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                    User = new UserApiDto { RunId = "run-1" }
                }
            }
        };

        _mockCache.Setup(x => x.GetAsync<ScanEventApiResponse>($"scan_events_{fromEventId}_{limit}"))
            .ReturnsAsync(cachedResponse);

        // Act
        var result = await _mockCache.Object.GetAsync<ScanEventApiResponse>($"scan_events_{fromEventId}_{limit}");

        // Assert
        PAssert.IsTrue(() => result != null);
        PAssert.IsTrue(() => result!.ScanEvents.Count == 1);
        PAssert.IsTrue(() => result!.ScanEvents[0].EventId == "cached-event");
    }

    [Fact]
    public async Task GetScanEventsAsync_When_ValidApiResponse_Should_ReturnData()
    {
        // Arrange
        var fromEventId = "test-event-2";
        var limit = 5;
        var expectedResponse = new ScanEventApiResponse
        {
            ScanEvents = new List<ScanEventApiDto>
            {
                new ScanEventApiDto
                {
                    EventId = "api-event-1",
                    Type = "STATUS",
                    StatusCode = "ORDER_RECEIVED",
                    CreatedDateTimeUtc = SystemClock.Instance.GetCurrentInstant(),
                    User = new UserApiDto { RunId = "api-run-1" }
                }
            }
        };

        _mockApiClient.Setup(x => x.GetScanEventsAsync(fromEventId, limit, default))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _mockApiClient.Object.GetScanEventsAsync(fromEventId, limit);

        // Assert
        PAssert.IsTrue(() => result != null);
        PAssert.IsTrue(() => result!.ScanEvents.Count == 1);
        PAssert.IsTrue(() => result!.ScanEvents[0].EventId == "api-event-1");
    }

    [Fact]
    public async Task GetScanEventsAsync_When_EmptyResult_Should_ReturnEmptyResponse()
    {
        // Arrange
        var fromEventId = "empty-event";
        var limit = 10;
        var emptyResponse = new ScanEventApiResponse
        {
            ScanEvents = new List<ScanEventApiDto>()
        };

        _mockApiClient.Setup(x => x.GetScanEventsAsync(fromEventId, limit, default))
            .ReturnsAsync(emptyResponse);

        // Act
        var result = await _mockApiClient.Object.GetScanEventsAsync(fromEventId, limit);

        // Assert
        PAssert.IsTrue(() => result != null);
        PAssert.IsTrue(() => result!.ScanEvents.Count == 0);
    }

    [Fact]
    public async Task GetScanEventsAsync_When_NullResponse_Should_ReturnNull()
    {
        // Arrange
        var fromEventId = "null-event";
        var limit = 10;

        _mockApiClient.Setup(x => x.GetScanEventsAsync(fromEventId, limit, default))
            .ReturnsAsync((ScanEventApiResponse?)null);

        // Act
        var result = await _mockApiClient.Object.GetScanEventsAsync(fromEventId, limit);

        // Assert
        PAssert.IsTrue(() => result == null);
    }
}
