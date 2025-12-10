// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;
using static PowerDisplay.Common.Drivers.PInvoke;

namespace PowerDisplay.Common.Drivers.DDC
{
    /// <summary>
    /// Manages physical monitor handles - reuse, cleanup, and validation
    /// </summary>
    public partial class PhysicalMonitorHandleManager : IDisposable
    {
        // Mapping: monitorId -> physical handle (thread-safe)
        private readonly LockedDictionary<string, IntPtr> _monitorIdToHandleMap = new();
        private bool _disposed;

        /// <summary>
        /// Get physical handle for monitor using its unique Id
        /// </summary>
        public IntPtr GetPhysicalHandle(Monitor monitor)
        {
            // Primary lookup: use monitor Id
            if (!string.IsNullOrEmpty(monitor.Id) &&
                _monitorIdToHandleMap.TryGetValue(monitor.Id, out var handle))
            {
                return handle;
            }

            // Fallback: use direct handle from monitor object
            if (monitor.Handle != IntPtr.Zero)
            {
                return monitor.Handle;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Update the handle mapping with new handles
        /// </summary>
        public void UpdateHandleMap(Dictionary<string, IntPtr> newHandleMap)
        {
            _monitorIdToHandleMap.ExecuteWithLock(dict =>
            {
                // Clean up unused handles before updating
                CleanupUnusedHandles(dict, newHandleMap);

                // Update the device key map
                dict.Clear();
                foreach (var kvp in newHandleMap)
                {
                    dict[kvp.Key] = kvp.Value;
                }
            });
        }

        /// <summary>
        /// Clean up handles that are no longer in use.
        /// Called within ExecuteWithLock context with the internal dictionary.
        /// Optimized to O(n) using HashSet lookup instead of O(n*m) nested loops.
        /// </summary>
        private void CleanupUnusedHandles(Dictionary<string, IntPtr> currentHandles, Dictionary<string, IntPtr> newHandles)
        {
            if (currentHandles.Count == 0)
            {
                return;
            }

            // Build HashSet of handles that will be reused (O(m))
            var reusedHandles = new HashSet<IntPtr>(newHandles.Values);

            // Find handles to destroy: in old map but not reused (O(n) with O(1) lookup)
            var handlesToDestroy = new List<IntPtr>();
            foreach (var oldHandle in currentHandles.Values)
            {
                if (oldHandle != IntPtr.Zero && !reusedHandles.Contains(oldHandle))
                {
                    handlesToDestroy.Add(oldHandle);
                }
            }

            // Destroy unused handles
            foreach (var handle in handlesToDestroy)
            {
                try
                {
                    DestroyPhysicalMonitor(handle);
                    Logger.LogDebug($"DDC: Cleaned up unused handle 0x{handle:X}");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"DDC: Failed to destroy handle 0x{handle:X}: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Release all physical monitor handles - get snapshot to avoid holding lock during cleanup
            var handles = _monitorIdToHandleMap.GetValuesSnapshot();
            foreach (var handle in handles)
            {
                if (handle != IntPtr.Zero)
                {
                    try
                    {
                        DestroyPhysicalMonitor(handle);
                        Logger.LogDebug($"Released physical monitor handle 0x{handle:X}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Failed to destroy physical monitor handle 0x{handle:X}: {ex.Message}");
                    }
                }
            }

            _monitorIdToHandleMap.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
