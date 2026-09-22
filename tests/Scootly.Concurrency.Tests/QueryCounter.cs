using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Scootly.Concurrency.Tests;

public static class QueryCounter
{
    public static int Count { get; private set; }
    public static int InterceptorCreatedCount { get; private set; }

    public static void Reset() => Count = 0;

    public sealed class CountingInterceptor : DbCommandInterceptor
    {
        public CountingInterceptor()
        {
            InterceptorCreatedCount++;
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Count++;
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}