using System;
using System.Threading;
using System.Threading.Tasks;
using Scan_Event_NoSQL.Models;

namespace Scan_Event_NoSQL.Services;

public interface IScanEventApiClient
{
    Task<ScanEventApiResponse?> GetScanEventsAsync(string? fromEventId = null, int limit = 100,
        CancellationToken cancellationToken = default);
}