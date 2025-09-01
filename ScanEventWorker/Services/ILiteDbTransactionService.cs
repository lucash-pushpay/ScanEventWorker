using System;
using System.Threading.Tasks;

namespace Scan_Event_NoSQL.Services;

public interface ILiteDbTransactionService
{
    Task ExecuteInTransactionAsync(Func<Task> operation);
    void ExecuteInTransaction(Action operation);
}