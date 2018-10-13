using System;
using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.AzureBlobStorage
{
    public static class TaskExtensions
    {
        public static bool SyncContextSafeWait(this Task task, int timeout = Timeout.Infinite)
        {
            var prevContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                // Wait so that the timer thread stays busy and thus
                // we know we're working when flushing.
                return task.Wait(timeout);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevContext);
            }
        }

        public static T SyncContextSafeWait<T>(this Task<T> task, int timeout = Timeout.Infinite)
        {
            var prevContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                // Wait so that the timer thread stays busy and thus
                // we know we're working when flushing.
                if (task.Wait(timeout))
                {
                    return task.Result;
                }
                else
                {
                    throw new TimeoutException("Operation failed to complete within allotted time.");
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevContext);
            }
        }
    }
}
