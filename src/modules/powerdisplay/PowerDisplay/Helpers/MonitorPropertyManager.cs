// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Serializes monitor property updates to prevent race conditions.
    /// Smart error handling: only rollback if the last operation in queue fails.
    /// </summary>
    public class MonitorPropertyManager : IDisposable
    {
        private readonly SemaphoreSlim _operationSemaphore = new(1, 1);
        private readonly string _monitorId;
        private readonly string _propertyName;
        private readonly object _taskLock = new object(); // Lock only for task creation
        private int _pendingValue = -1; // Value waiting to be executed
        private int _hasPendingValue = 0; // 0 = no pending, 1 = has pending (for Interlocked)
        private Task? _currentTask;
        
        // Track last successful value for smart rollback
        private int _lastSuccessfulValue = -1;
        private bool _hasLastSuccessfulValue = false;
        
        public MonitorPropertyManager(string monitorId, string propertyName)
        {
            _monitorId = monitorId;
            _propertyName = propertyName;
        }

        /// <summary>
        /// Queue a property update - replaces any pending update
        /// </summary>
        public void QueueUpdate(int newValue, Func<int, CancellationToken, Task<bool>> updateAction)
        {
            // Atomically update the pending value (lock-free for UI thread)
            Interlocked.Exchange(ref _pendingValue, newValue);
            Interlocked.Exchange(ref _hasPendingValue, 1);
            
            // If no operation is currently running, start one (lock only task creation)
            if (_currentTask == null || _currentTask.IsCompleted)
            {
                lock (_taskLock)
                {
                    // Double-check inside lock
                    if (_currentTask == null || _currentTask.IsCompleted)
                    {
                        _currentTask = ExecuteUpdatesAsync(updateAction);
                    }
                }
            }
        }
        
        /// <summary>
        /// Execute queued updates with smart error handling:
        /// - Continue executing even if intermediate operations fail
        /// - Only rollback if the LAST operation fails
        /// - Rollback to the last successful value
        /// </summary>
        private async Task ExecuteUpdatesAsync(Func<int, CancellationToken, Task<bool>> updateAction)
        {
            int? lastAttemptedValue = null;
            bool lastAttemptSuccess = false;
            
            while (true)
            {
                // Atomically check and retrieve the next value to update (lock-free)
                if (Interlocked.Exchange(ref _hasPendingValue, 0) == 0)
                {
                    // No more updates pending - check if we need to rollback
                    if (lastAttemptedValue.HasValue && !lastAttemptSuccess)
                    {
                        // Last operation failed - trigger rollback
                        if (_hasLastSuccessfulValue)
                        {
                            Logger.LogWarning($"[{_monitorId}] {_propertyName} last operation failed, should rollback to {_lastSuccessfulValue}");
                            // Return the rollback value so caller can update UI
                            RollbackRequested?.Invoke(this, _lastSuccessfulValue);
                        }
                    }
                    break;
                }
                
                int valueToUpdate = Volatile.Read(ref _pendingValue);
                lastAttemptedValue = valueToUpdate;
                
                // Execute the hardware update
                try
                {
                    await _operationSemaphore.WaitAsync();
                    
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    bool success = await updateAction(valueToUpdate, cts.Token);
                    
                    if (success)
                    {
                        lastAttemptSuccess = true;
                        _lastSuccessfulValue = valueToUpdate;
                        _hasLastSuccessfulValue = true;
                        Logger.LogDebug($"[{_monitorId}] {_propertyName} successfully updated to {valueToUpdate}");
                    }
                    else
                    {
                        lastAttemptSuccess = false;
                        Logger.LogWarning($"[{_monitorId}] {_propertyName} failed to update to {valueToUpdate}, continuing with queue...");
                    }
                }
                catch (Exception ex)
                {
                    lastAttemptSuccess = false;
                    Logger.LogError($"[{_monitorId}] Exception updating {_propertyName} to {valueToUpdate}: {ex.Message}");
                }
                finally
                {
                    _operationSemaphore.Release();
                }
            }
        }
        
        /// <summary>
        /// Event triggered when rollback is needed (only when last operation fails)
        /// </summary>
        public event EventHandler<int>? RollbackRequested;
        
        /// <summary>
        /// Wait for all pending updates to complete
        /// </summary>
        public async Task FlushAsync()
        {
            var currentTask = _currentTask;
            if (currentTask != null && !currentTask.IsCompleted)
            {
                try
                {
                    await currentTask;
                }
                catch
                {
                    // Ignore errors during flush
                }
            }
        }
        
        public void Dispose()
        {
            _operationSemaphore?.Dispose();
        }
    }
}