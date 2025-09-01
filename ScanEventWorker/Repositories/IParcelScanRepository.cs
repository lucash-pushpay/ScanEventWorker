using System.Collections.Generic;
using System.Threading.Tasks;
using Scan_Event_NoSQL.Models;

namespace Scan_Event_NoSQL.Repositories;

public interface IParcelScanRepository
{
    Task UpdateParcelsWithScanEventsAsync(List<ScanEvent> scanEvents);
}