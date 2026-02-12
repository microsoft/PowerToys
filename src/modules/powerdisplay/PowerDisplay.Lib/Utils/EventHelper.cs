// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using ManagedCommon;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Helper class for Windows named event operations.
    /// Provides unified event signaling with consistent error handling and logging.
    /// </summary>
    public static class EventHelper
    {
        /// <summary>
        /// Signals a named event. Creates the event if it doesn't exist.
        /// </summary>
        /// <param name="eventName">The name of the event to signal.</param>
        /// <returns>True if the event was signaled successfully, false otherwise.</returns>
        public static bool SignalEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                Logger.LogWarning("[EventHelper] SignalEvent called with null or empty event name");
                return false;
            }

            try
            {
                using var eventHandle = new EventWaitHandle(
                    false,
                    EventResetMode.AutoReset,
                    eventName);
                eventHandle.Set();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[EventHelper] Failed to signal event '{eventName}': {ex.Message}");
                return false;
            }
        }
    }
}
