// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ManagedCommon;
using static PowerDisplay.Common.Drivers.PInvoke;

namespace PowerDisplay.Common.Drivers.DDC
{
    /// <summary>
    /// Manages physical monitor handles - reuse, cleanup, and validation
    /// </summary>
    public partial class PhysicalMonitorHandleManager : IDisposable
    {
        // Mapping: monitorId -> physical handle (thread-safe)
        private readonly ConcurrentDictionary<string, IntPtr> _monitorIdToHandleMap = new();
        private readonly object _handleLock = new();
        private bool _disposed;

        /// <summary>
        /// Update the handle mapping with new handles
        /// </summary>
        public void UpdateHandleMap(Dictionary<string, IntPtr> newHandleMap)
        {
            // Lock to ensure atomic update (cleanup + replace)
            lock (_handleLock)
            {
                // Clean up unused handles before updating
                CleanupUnusedHandles(newHandleMap);

                // Update the device key map
                _monitorIdToHandleMap.Clear();
                foreach (var kvp in newHandleMap)
                {
                    _monitorIdToHandleMap[kvp.Key] = kvp.Value;
                }
            }
        }

        /// <summary>
        /// Clean up handles that are no longer in use.
        /// Called within lock context. Optimized to O(n) using HashSet lookup.
        /// </summary>
        private void CleanupUnusedHandles(Dictionary<string, IntPtr> newHandles)
        {
            if (_monitorIdToHandleMap.IsEmpty)
            {
                return;
            }

            // Build HashSet of handles that will be reused (O(m))
            var reusedHandles = new HashSet<IntPtr>(newHandles.Values);

            // Find handles to destroy: in old map but not reused (O(n) with O(1) lookup)
            var handlesToDestroy = _monitorIdToHandleMap.Values
                .Where(h => h != IntPtr.Zero && !reusedHandles.Contains(h))
                .ToList();

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
            var handles = _monitorIdToHandleMap.Values.ToList();
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
