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
    /// Simple async property updater - UI updates immediately, hardware updates use latest value.
    /// When hardware operation completes, immediately applies the latest queued value if changed.
    /// No debounce delay - just serial execution with latest value.
    /// </summary>
    public class MonitorPropertyManager : IDisposable
    {
        private readonly SemaphoreSlim _operationSemaphore = new(1, 1);
        private readonly string _monitorId;
        private readonly string _propertyName;
        private readonly object _stateLock = new object();
        
        private int _currentValue = -1;  // Value currently applied to hardware  
        private int _targetValue = -1;   // Latest value user wants
        private bool _isRunning = false; // Is update task running
        
        public MonitorPropertyManager(string monitorId, string propertyName)
        {
            _monitorId = monitorId;
            _propertyName = propertyName;
        }

        /// <summary>
        /// Queue a property update - UI thread friendly (non-blocking)
        /// </summary>
        public void QueueUpdate(int newValue, Func<int, CancellationToken, Task<bool>> updateAction)
        {
            bool shouldStartTask = false;
            
            lock (_stateLock)
            {
                _targetValue = newValue;
                
                // Only start new task if no task is currently running
                if (!_isRunning)
                {
                    _isRunning = true;
                    shouldStartTask = true;
                }
            }
            
            // Start update task if needed (outside lock)
            if (shouldStartTask)
            {
                _ = Task.Run(async () =>
                {
                    await _operationSemaphore.WaitAsync();
                    try
                    {
                        await ExecuteUpdatesAsync(updateAction);
                    }
                    finally
                    {
                        lock (_stateLock)
                        {
                            _isRunning = false;
                        }
                        _operationSemaphore.Release();
                    }
                });
            }
        }
        
        /// <summary>
        /// Execute updates until target value matches current value
        /// No debounce delay - immediately applies the latest target value after current operation completes
        /// </summary>
        private async Task ExecuteUpdatesAsync(Func<int, CancellationToken, Task<bool>> updateAction)
        {
            int consecutiveFailures = 0;
            const int MaxConsecutiveFailures = 3; // 最多连续失败3次就放弃
            
            while (true)
            {
                int valueToApply;
                
                // Check if there's a new value to apply
                lock (_stateLock)
                {
                    if (_targetValue == _currentValue)
                    {
                        // Target matches current, no update needed
                        return;
                    }
                    
                    valueToApply = _targetValue;
                }
                
                // Execute hardware update (outside lock)
                try
                {
                    ManagedCommon.Logger.LogDebug($"[{_monitorId}] {_propertyName} applying value {valueToApply}");
                    
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    bool success = await updateAction(valueToApply, cts.Token);
                    
                    if (success)
                    {
                        lock (_stateLock)
                        {
                            _currentValue = valueToApply;
                        }
                        ManagedCommon.Logger.LogDebug($"[{_monitorId}] {_propertyName} successfully updated to {valueToApply}");
                        consecutiveFailures = 0; // 成功后重置失败计数
                    }
                    else
                    {
                        consecutiveFailures++;
                        ManagedCommon.Logger.LogWarning($"[{_monitorId}] {_propertyName} failed to update to {valueToApply} (attempt {consecutiveFailures}/{MaxConsecutiveFailures})");
                        
                        if (consecutiveFailures >= MaxConsecutiveFailures)
                        {
                            ManagedCommon.Logger.LogError($"[{_monitorId}] {_propertyName} failed {MaxConsecutiveFailures} times, giving up. Hardware may not support this feature.");
                            // 放弃：假装成功，避免无限循环
                            lock (_stateLock)
                            {
                                _currentValue = valueToApply;
                            }
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    consecutiveFailures++;
                    ManagedCommon.Logger.LogError($"[{_monitorId}] {_propertyName} update exception: {ex.Message} (attempt {consecutiveFailures}/{MaxConsecutiveFailures})");
                    
                    if (consecutiveFailures >= MaxConsecutiveFailures)
                    {
                        ManagedCommon.Logger.LogError($"[{_monitorId}] {_propertyName} exception {MaxConsecutiveFailures} times, giving up. Hardware may not support this feature.");
                        // 放弃：假装成功，避免无限循环
                        lock (_stateLock)
                        {
                            _currentValue = valueToApply;
                        }
                        return;
                    }
                }
                
                // Loop back to check if target changed during the update
            }
        }
        
        /// <summary>
        /// Wait for all pending updates to complete
        /// </summary>
        public async Task FlushAsync()
        {
            // Wait for operation semaphore to ensure all updates completed
            await _operationSemaphore.WaitAsync();
            _operationSemaphore.Release();
        }
        
        public void Dispose()
        {
            _operationSemaphore?.Dispose();
        }
    }
}