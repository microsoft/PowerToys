// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Library;

namespace RunnerV2.Helpers
{
    internal static class HotkeyConflictsManager
    {
        public sealed record HotkeyConflict(HotkeySettings Hotkey, string ModuleName, int HotkeyID);

        private static readonly Lock _hotkeyLock = new();

        private static readonly Dictionary<int, List<HotkeyConflict>> _sysConflictHotkeys = [];
        private static readonly Dictionary<int, List<HotkeyConflict>> _inAppConflictHotkeys = [];

        internal static bool HasConflict(HotkeySettings hotkey)
        {
            _hotkeyLock.Enter();
            if (hotkey.IsEmpty())
            {
                _hotkeyLock.Exit();
                return false;
            }

            if (HasConflictWithSystemHotkey(hotkey))
            {
                _hotkeyLock.Exit();
                return true;
            }

            var modulesWithSameHotkey = CentralizedKeyboardHookManager.GetAllModulesWithShortcut(hotkey);
            _hotkeyLock.Exit();
            return modulesWithSameHotkey.Count > 0;
        }

        internal static List<HotkeyConflict> GetAllConflicts(HotkeySettings hotkey)
        {
            _hotkeyLock.Enter();
            List<HotkeyConflict> conflicts = [];
            if (hotkey.IsEmpty())
            {
                _hotkeyLock.Exit();
                return conflicts;
            }

            if (HasConflictWithSystemHotkey(hotkey))
            {
                conflicts.Add(new HotkeyConflict(hotkey, "System", -1));
            }

            conflicts.AddRange(_inAppConflictHotkeys.GetValueOrDefault(hotkey.GetHashCode(), []));

            _hotkeyLock.Exit();
            return conflicts;
        }

        internal static JsonNode GetHotkeyConflictsAsJson()
        {
            _hotkeyLock.Enter();

            JsonNode hotkeyConflicts = new JsonObject();

            static JsonObject SerializeShortcut(HotkeySettings hotkey) =>
                new()
                {
                    ["win"] = hotkey.Win,
                    ["ctrl"] = hotkey.Ctrl,
                    ["alt"] = hotkey.Alt,
                    ["shift"] = hotkey.Shift,
                    ["key"] = hotkey.Code,
                };

            JsonArray inAppConflictsArray = [];
            JsonArray sysConflictsArray = [];

            foreach (List<HotkeyConflict> conflicts in _inAppConflictHotkeys.Values)
            {
                if (conflicts.Count == 0)
                {
                    continue;
                }

                JsonObject conflictGroup = [];

                conflictGroup["hotkey"] = SerializeShortcut(conflicts[0].Hotkey);

                JsonArray modules = [];
                foreach (HotkeyConflict conflict in conflicts)
                {
                    JsonObject moduleInfo = [];
                    moduleInfo["moduleName"] = conflict.ModuleName;
                    moduleInfo["hotkeyID"] = conflict.HotkeyID;
                    modules.Add(moduleInfo);
                }

                conflictGroup["modules"] = modules;
                inAppConflictsArray.Add(conflictGroup);
            }

            foreach (List<HotkeyConflict> conflicts in _sysConflictHotkeys.Values)
            {
                if (conflicts.Count == 0)
                {
                    continue;
                }

                JsonObject conflictGroup = [];

                conflictGroup["hotkey"] = SerializeShortcut(conflicts[0].Hotkey);

                JsonArray modules = [];
                foreach (HotkeyConflict conflict in conflicts)
                {
                    JsonObject moduleInfo = [];
                    moduleInfo["moduleName"] = conflict.ModuleName;
                    moduleInfo["hotkeyID"] = conflict.HotkeyID;
                    modules.Add(moduleInfo);
                }

                conflictGroup["modules"] = modules;
                sysConflictsArray.Add(conflictGroup);
            }

            hotkeyConflicts.Root["inAppConflicts"] = inAppConflictsArray;
            hotkeyConflicts.Root["sysConflicts"] = sysConflictsArray;

            _hotkeyLock.Exit();
            return hotkeyConflicts;
        }

        internal static void AddHotkey(HotkeySettings hotkey, string moduleName, int hotkeyID)
        {
            switch (HasConflict(hotkey, moduleName))
            {
                case ConflictType.InApp:
                    if (!_inAppConflictHotkeys.ContainsKey(hotkey.GetHashCode()))
                    {
                        _inAppConflictHotkeys[hotkey.GetHashCode()] = [];
                    }

                    _inAppConflictHotkeys[hotkey.GetHashCode()].Add(new HotkeyConflict(hotkey, moduleName, hotkeyID));
                    break;
                case ConflictType.System:
                    // PowerToys Run has own keyboard hook
                    if (moduleName == "PowerToys Run")
                    {
                        break;
                    }

                    if (!_sysConflictHotkeys.ContainsKey(hotkey.GetHashCode()))
                    {
                        _sysConflictHotkeys[hotkey.GetHashCode()] = [];
                    }

                    _sysConflictHotkeys[hotkey.GetHashCode()].Add(new HotkeyConflict(hotkey, moduleName, hotkeyID));
                    break;
                case ConflictType.None:
                default:
                    break;
            }
        }

        internal static void RemoveHotkeysOfModule(string moduleName)
        {
            _hotkeyLock.Enter();
            foreach (List<HotkeyConflict> conflicts in _inAppConflictHotkeys.Values)
            {
                conflicts.RemoveAll(conflict => conflict.ModuleName == moduleName);
            }

            foreach (List<HotkeyConflict> conflicts in _sysConflictHotkeys.Values)
            {
                conflicts.RemoveAll(conflict => conflict.ModuleName == moduleName);
            }

            _hotkeyLock.Exit();
        }

        protected enum ConflictType
        {
            None,
            InApp,
            System,
        }

        private static ConflictType HasConflict(HotkeySettings hotkey, string moduleName)
        {
            _hotkeyLock.Enter();

            if (hotkey.IsEmpty())
            {
                _hotkeyLock.Exit();
                return ConflictType.None;
            }

            if (HasConflictWithSystemHotkey(hotkey))
            {
                _hotkeyLock.Exit();
                return ConflictType.System;
            }

            var modulesWithSameHotkey = CentralizedKeyboardHookManager.GetAllModulesWithShortcut(hotkey);
            modulesWithSameHotkey.Remove(moduleName); // Remove the current module from the list to avoid false positive

            if (modulesWithSameHotkey.Count > 0)
            {
                _hotkeyLock.Exit();
                return ConflictType.InApp;
            }

            _hotkeyLock.Exit();
            return ConflictType.None;
        }

        private static bool HasConflictWithSystemHotkey(HotkeySettings hotkey)
        {
            if (hotkey.IsEmpty())
            {
                return false;
            }

            uint modifiers = (uint)((hotkey.Win ? 0x0008 : 0) |
                             (hotkey.Ctrl ? 0x0002 : 0) |
                             (hotkey.Alt ? 0x0001 : 0) |
                             (hotkey.Shift ? 0x0004 : 0));

            // Use a unique ID for this test registration
            int hotkeyId = 0x0FFF;

            if (!NativeMethods.RegisterHotKey(IntPtr.Zero, hotkeyId, modifiers, (uint)hotkey.Code))
            {
                if (Marshal.GetLastWin32Error() == 1409/* ERROR_HOTKEY_ALREADY_REGISTERED */)
                {
                    return true;
                }
            }
            else
            {
                NativeMethods.UnregisterHotKey(IntPtr.Zero, hotkeyId);
            }

            return false;
        }
    }
}
