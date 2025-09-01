using System.Collections.Generic;
using System.Threading.Tasks;
using Scan_Event_NoSQL.Models;

namespace Scan_Event_NoSQL.Services;

public interface IScanEventProcessingService
{
    Task ProcessScanEventBatchAsync(IEnumerable<ScanEventApiDto> apiEvents);
}