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
    /// Serializes monitor property updates to prevent race conditions
    /// Simple approach: one operation at a time, newest replaces pending
    /// </summary>
    public class MonitorPropertyManager : IDisposable
    {
        private readonly SemaphoreSlim _operationSemaphore = new(1, 1);
        private int _pendingValue = -1; // Value waiting to be executed
        private bool _hasPendingValue = false;
        private Task? _currentTask;
        private readonly string _monitorId;
        private readonly string _propertyName;
        
        public MonitorPropertyManager(string monitorId, string propertyName)
        {
            _monitorId = monitorId;
            _propertyName = propertyName;
        }

        /// <summary>
        /// Queue a property update - replaces any pending update
        /// </summary>
        public void QueueUpdate(int newValue, Func<int, CancellationToken, Task> updateAction)
        {
            lock (this)
            {
                // Always update the pending value (newest wins)
                _pendingValue = newValue;
                _hasPendingValue = true;
                
                // If no operation is currently running, start one
                if (_currentTask == null || _currentTask.IsCompleted)
                {
                    _currentTask = ExecuteUpdatesAsync(updateAction);
                }
            }
        }
        
        private async Task ExecuteUpdatesAsync(Func<int, CancellationToken, Task> updateAction)
        {
            while (true)
            {
                int valueToUpdate;
                
                // Get the next value to update
                lock (this)
                {
                    if (!_hasPendingValue)
                    {
                        // No more updates pending
                        break;
                    }
                    
                    valueToUpdate = _pendingValue;
                    _hasPendingValue = false;
                }
                
                // Execute the hardware update
                try
                {
                    await _operationSemaphore.WaitAsync();
                    
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    await updateAction(valueToUpdate, cts.Token);
                    
                    Logger.LogDebug($"[{_monitorId}] {_propertyName} updated to {valueToUpdate}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[{_monitorId}] Failed to update {_propertyName} to {valueToUpdate}: {ex.Message}");
                }
                finally
                {
                    _operationSemaphore.Release();
                }
            }
        }
        
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