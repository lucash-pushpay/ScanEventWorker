using System;
using System.Threading.Tasks;
using Scan_Event_NoSQL.Models;

namespace Scan_Event_NoSQL.Repositories;

public interface IProcessingStateRepository
{
    Task<string?> GetLastProcessedEventIdAsync();
    Task SaveLastProcessedEventIdAsync(string? eventId);
}