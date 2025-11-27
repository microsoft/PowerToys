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
        // Mapping: deviceKey -> physical handle (thread-safe)
        private readonly LockedDictionary<string, IntPtr> _deviceKeyToHandleMap = new();
        private bool _disposed;

        /// <summary>
        /// Get physical handle for monitor using stable deviceKey
        /// </summary>
        public IntPtr GetPhysicalHandle(Monitor monitor)
        {
            // Primary lookup: use stable deviceKey from EnumDisplayDevices
            if (!string.IsNullOrEmpty(monitor.DeviceKey) &&
                _deviceKeyToHandleMap.TryGetValue(monitor.DeviceKey, out var handle))
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
        /// Try to reuse existing handle if valid, otherwise use new handle
        /// Returns the handle to use and whether it was reused
        /// </summary>
        public (IntPtr Handle, bool WasReused) ReuseOrCreateHandle(string deviceKey, IntPtr newHandle)
        {
            if (string.IsNullOrEmpty(deviceKey))
            {
                return (newHandle, false);
            }

            return _deviceKeyToHandleMap.ExecuteWithLock(dict =>
            {
                // Try to reuse existing handle if it's still valid
                // Use quick connection check instead of full capabilities retrieval
                if (dict.TryGetValue(deviceKey, out var existingHandle) &&
                    existingHandle != IntPtr.Zero &&
                    DdcCiNative.QuickConnectionCheck(existingHandle))
                {
                    // Destroy the newly created handle since we're using the old one
                    if (newHandle != existingHandle && newHandle != IntPtr.Zero)
                    {
                        DestroyPhysicalMonitor(newHandle);
                    }

                    return (existingHandle, true);
                }

                return (newHandle, false);
            });
        }

        /// <summary>
        /// Update the handle mapping with new handles
        /// </summary>
        public void UpdateHandleMap(Dictionary<string, IntPtr> newHandleMap)
        {
            _deviceKeyToHandleMap.ExecuteWithLock(dict =>
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
        /// </summary>
        private void CleanupUnusedHandles(Dictionary<string, IntPtr> currentHandles, Dictionary<string, IntPtr> newHandles)
        {
            if (currentHandles.Count == 0)
            {
                return;
            }

            var handlesToDestroy = new List<IntPtr>();

            // Find handles that are in old map but not being reused
            foreach (var oldMapping in currentHandles)
            {
                bool found = false;
                foreach (var newMapping in newHandles)
                {
                    // If the same handle is being reused, don't destroy it
                    if (oldMapping.Value == newMapping.Value)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found && oldMapping.Value != IntPtr.Zero)
                {
                    handlesToDestroy.Add(oldMapping.Value);
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
            var handles = _deviceKeyToHandleMap.GetValuesSnapshot();
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

            _deviceKeyToHandleMap.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
