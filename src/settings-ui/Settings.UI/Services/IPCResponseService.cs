// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Views;
using Windows.Data.Json;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    public class IPCResponseService
    {
        private static IPCResponseService _instance;

        public static IPCResponseService Instance => _instance ??= new IPCResponseService();

        public void RegisterForIPC()
        {
            ShellPage.ShellHandler?.IPCResponseHandleList.Add(ProcessIPCMessage);
        }

        public void UnregisterFromIPC()
        {
            ShellPage.ShellHandler?.IPCResponseHandleList.Remove(ProcessIPCMessage);
        }

        private void ProcessIPCMessage(JsonObject json)
        {
            try
            {
                if (json.TryGetValue("response_type", out IJsonValue responseTypeValue) &&
                responseTypeValue.ValueType == JsonValueType.String &&
                responseTypeValue.GetString().Equals("hotkey_conflict_result", StringComparison.Ordinal))
                {
                    string requestId = string.Empty;
                    if (json.TryGetValue("request_id", out IJsonValue requestIdValue) &&
                        requestIdValue.ValueType == JsonValueType.String)
                    {
                        requestId = requestIdValue.GetString();
                    }

                    bool hasConflict = false;
                    if (json.TryGetValue("has_conflict", out IJsonValue hasConflictValue) &&
                        hasConflictValue.ValueType == JsonValueType.Boolean)
                    {
                        hasConflict = hasConflictValue.GetBoolean();
                    }

                    string conflictModuleName = string.Empty;
                    string conflictHotkeyName = string.Empty;

                    if (hasConflict)
                    {
                        if (json.TryGetValue("conflict_module", out IJsonValue conflictModuleValue) &&
                            conflictModuleValue.ValueType == JsonValueType.String)
                        {
                            conflictModuleName = conflictModuleValue.GetString();
                        }

                        if (json.TryGetValue("conflict_hotkey_name", out IJsonValue conflictHotkeyValue) &&
                            conflictHotkeyValue.ValueType == JsonValueType.String)
                        {
                            conflictHotkeyName = conflictHotkeyValue.GetString();
                        }
                    }

                    var response = new HotkeyConflictResponse
                    {
                        RequestId = requestId,
                        HasConflict = hasConflict,
                        ConflictModuleName = conflictModuleName,
                        ConflictHotkeyName = conflictHotkeyName,
                    };

                    HotkeyConflictHelper.HandleHotkeyConflictResponse(response);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
