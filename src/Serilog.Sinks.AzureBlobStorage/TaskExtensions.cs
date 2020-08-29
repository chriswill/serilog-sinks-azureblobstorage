// Copyright 2018 CloudScope, LLC
// Portions copyright 2014 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.AzureBlobStorage
{
    public static class TaskExtensions
    {
        public static bool SyncContextSafeWait(this Task task, int timeout = Timeout.Infinite)
        {
            SynchronizationContext prevContext = SynchronizationContext.Current;
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
            SynchronizationContext prevContext = SynchronizationContext.Current;
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
