using System;
using System.Threading.Tasks;
using LiteDB;
using Serilog;

namespace Scan_Event_NoSQL.Services;

public class LiteDbTransactionService(LiteDatabase database) : ILiteDbTransactionService
{
    private readonly ILogger _logger = Log.ForContext<LiteDbTransactionService>();

    public async Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        // Since LiteDB transactions are synchronous, we need to handle this carefully
        // We run the transaction logic synchronously but allow async operations within
        await Task.Run(() =>
        {
            database.BeginTrans();
            try
            {
                // Execute the async operation synchronously within the transaction context
                // This is safe because we're in a Task.Run context
                operation().GetAwaiter().GetResult();
                database.Commit();
                _logger.Information("Transaction committed successfully");
            }
            catch (Exception ex)
            {
                database.Rollback();
                _logger.Error(ex, "Transaction rolled back due to error: {ErrorMessage}", ex.Message);
                throw;
            }
        });
    }

    public void ExecuteInTransaction(Action operation)
    {
        database.BeginTrans();
        try
        {
            operation();
            database.Commit();
            _logger.Information("Transaction committed successfully");
        }
        catch (Exception ex)
        {
            database.Rollback();
            _logger.Error(ex, "Transaction rolled back due to error: {ErrorMessage}", ex.Message);
            throw;
        }
    }
}