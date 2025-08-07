// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Views;
using Windows.Data.Json;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    public class IPCResponseService
    {
        private static IPCResponseService _instance;

        public static IPCResponseService Instance => _instance ??= new IPCResponseService();

        public static event EventHandler<AllHotkeyConflictsEventArgs> AllHotkeyConflictsReceived;

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
                    responseTypeValue.ValueType == JsonValueType.String)
                {
                    string responseType = responseTypeValue.GetString();

                    if (responseType.Equals("hotkey_conflict_result", StringComparison.Ordinal))
                    {
                        ProcessHotkeyConflictResult(json);
                    }
                    else if (responseType.Equals("all_hotkey_conflicts", StringComparison.Ordinal))
                    {
                        ProcessAllHotkeyConflicts(json);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void ProcessHotkeyConflictResult(JsonObject json)
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

            var allConflicts = new List<ModuleHotkeyData>();

            if (hasConflict)
            {
                // Parse the all_conflicts array
                if (json.TryGetValue("all_conflicts", out IJsonValue allConflictsValue) &&
                    allConflictsValue.ValueType == JsonValueType.Array)
                {
                    var conflictsArray = allConflictsValue.GetArray();
                    foreach (var conflictItem in conflictsArray)
                    {
                        if (conflictItem.ValueType == JsonValueType.Object)
                        {
                            var conflictObj = conflictItem.GetObject();

                            string moduleName = string.Empty;
                            int hotkeyID = -1;

                            if (conflictObj.TryGetValue("module", out IJsonValue moduleValue) &&
                                moduleValue.ValueType == JsonValueType.String)
                            {
                                moduleName = moduleValue.GetString();
                            }

                            if (conflictObj.TryGetValue("hotkeyID", out IJsonValue hotkeyValue) &&
                                hotkeyValue.ValueType == JsonValueType.Number)
                            {
                                hotkeyID = (int)hotkeyValue.GetNumber();
                            }

                            allConflicts.Add(new ModuleHotkeyData
                            {
                                ModuleName = moduleName,
                                HotkeyID = hotkeyID,
                            });
                        }
                    }
                }
            }

            var response = new HotkeyConflictResponse
            {
                RequestId = requestId,
                HasConflict = hasConflict,
                AllConflicts = allConflicts,
            };

            HotkeyConflictHelper.HandleHotkeyConflictResponse(response);
        }

        private void ProcessAllHotkeyConflicts(JsonObject json)
        {
            var allConflicts = new AllHotkeyConflictsData();

            if (json.TryGetValue("inAppConflicts", out IJsonValue inAppValue) &&
                inAppValue.ValueType == JsonValueType.Array)
            {
                var inAppArray = inAppValue.GetArray();
                foreach (var conflictGroup in inAppArray)
                {
                    var conflictObj = conflictGroup.GetObject();
                    var conflictData = ParseConflictGroup(conflictObj, false);
                    if (conflictData != null)
                    {
                        allConflicts.InAppConflicts.Add(conflictData);
                    }
                }
            }

            if (json.TryGetValue("sysConflicts", out IJsonValue sysValue) &&
                sysValue.ValueType == JsonValueType.Array)
            {
                var sysArray = sysValue.GetArray();
                foreach (var conflictGroup in sysArray)
                {
                    var conflictObj = conflictGroup.GetObject();
                    var conflictData = ParseConflictGroup(conflictObj, true);
                    if (conflictData != null)
                    {
                        allConflicts.SystemConflicts.Add(conflictData);
                    }
                }
            }

            AllHotkeyConflictsReceived?.Invoke(this, new AllHotkeyConflictsEventArgs(allConflicts));
        }

        private HotkeyConflictGroupData ParseConflictGroup(JsonObject conflictObj, bool isSystemConflict)
        {
            if (!conflictObj.TryGetValue("hotkey", out var hotkeyValue) ||
                !conflictObj.TryGetValue("modules", out var modulesValue))
            {
                return null;
            }

            var hotkeyObj = hotkeyValue.GetObject();
            bool win = hotkeyObj.TryGetValue("win", out var winVal) && winVal.GetBoolean();
            bool ctrl = hotkeyObj.TryGetValue("ctrl", out var ctrlVal) && ctrlVal.GetBoolean();
            bool shift = hotkeyObj.TryGetValue("shift", out var shiftVal) && shiftVal.GetBoolean();
            bool alt = hotkeyObj.TryGetValue("alt", out var altVal) && altVal.GetBoolean();
            int key = hotkeyObj.TryGetValue("key", out var keyVal) ? (int)keyVal.GetNumber() : 0;

            var conflictGroup = new HotkeyConflictGroupData
            {
                Hotkey = new HotkeyData { Win = win, Ctrl = ctrl, Shift = shift, Alt = alt, Key = key },
                IsSystemConflict = isSystemConflict,
                Modules = new List<ModuleHotkeyData>(),
            };

            var modulesArray = modulesValue.GetArray();
            foreach (var module in modulesArray)
            {
                var moduleObj = module.GetObject();
                string moduleName = moduleObj.TryGetValue("moduleName", out var modNameVal) ? modNameVal.GetString() : string.Empty;
                int hotkeyID = moduleObj.TryGetValue("hotkeyID", out var hotkeyIDVal) ? (int)hotkeyIDVal.GetNumber() : -1;

                conflictGroup.Modules.Add(new ModuleHotkeyData
                {
                    ModuleName = moduleName,
                    HotkeyID = hotkeyID,
                });
            }

            return conflictGroup;
        }
    }
}
