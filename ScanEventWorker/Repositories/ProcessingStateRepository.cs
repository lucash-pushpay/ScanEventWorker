using System;
using System.Threading.Tasks;
using LiteDB;
using NodaTime;
using Serilog;
using Scan_Event_NoSQL.Models;

namespace Scan_Event_NoSQL.Repositories;

public class ProcessingStateRepository(LiteDatabase database) : IProcessingStateRepository
{
    private readonly ILogger _logger = Log.ForContext<ProcessingStateRepository>();

    private readonly ILiteCollection<ProcessingState> _collection =
        database.GetCollection<ProcessingState>("processing_state");

    public async Task<string?> GetLastProcessedEventIdAsync()
    {
        return await Task.Run(() =>
        {
            var state = _collection.FindById("current_state");
            var lastProcessedEventId = state?.LastProcessedEventId;

            _logger.Information("Retrieved last processed event ID: {EventId}", lastProcessedEventId ?? "null");
            return lastProcessedEventId;
        });
    }

    public async Task SaveLastProcessedEventIdAsync(string? eventId)
    {
        await Task.Run(() =>
        {
            var state = _collection.FindById("current_state") ?? new ProcessingState
            {
                Id = "current_state"
            };

            state.LastProcessedEventId = eventId;
            state.LastProcessedAt = SystemClock.Instance.GetCurrentInstant();

            _collection.Upsert(state);
            _logger.Information("Saved last processed event ID: {EventId}", eventId);
        });
    }
}