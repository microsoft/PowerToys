// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public class HotkeyConflictHelper
    {
        public delegate void HotkeyConflictCheckCallback(bool hasConflict, HotkeyConflictResponse conflicts);

        private static readonly Dictionary<string, HotkeyConflictCheckCallback> PendingHotkeyConflictChecks = new Dictionary<string, HotkeyConflictCheckCallback>();
        private static readonly object LockObject = new object();

        public static string CheckHotkeyConflict(HotkeySettings hotkeySettings, Func<string, int> ipcMSGCallBackFunc, HotkeyConflictCheckCallback callback)
        {
            if (hotkeySettings == null || ipcMSGCallBackFunc == null)
            {
                return string.Empty;
            }

            string requestId = GenerateRequestId();

            lock (LockObject)
            {
                PendingHotkeyConflictChecks[requestId] = callback;
            }

            var hotkeyObj = new JsonObject
            {
                ["request_id"] = requestId,
                ["win"] = hotkeySettings.Win,
                ["ctrl"] = hotkeySettings.Ctrl,
                ["shift"] = hotkeySettings.Shift,
                ["alt"] = hotkeySettings.Alt,
                ["key"] = hotkeySettings.Code,
            };

            var requestObject = new JsonObject
            {
                ["check_hotkey_conflict"] = hotkeyObj,
            };

            ipcMSGCallBackFunc(requestObject.ToString());

            return requestId;
        }

        // Forgets a check whose answer never came, so that it does not stay in the list.
        public static void CancelHotkeyConflictCheck(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
            {
                return;
            }

            lock (LockObject)
            {
                PendingHotkeyConflictChecks.Remove(requestId);
            }
        }

        public static void HandleHotkeyConflictResponse(HotkeyConflictResponse response)
        {
            HotkeyConflictCheckCallback callback = null;

            lock (LockObject)
            {
                if (PendingHotkeyConflictChecks.TryGetValue(response.RequestId, out callback))
                {
                    PendingHotkeyConflictChecks.Remove(response.RequestId);
                }
            }

            // The answer is given whether there is a conflict or not, so that the caller can wait for it.
            callback?.Invoke(response.HasConflict, response);
        }

        private static string GenerateRequestId() => Guid.NewGuid().ToString();
    }
}
