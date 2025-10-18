// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    public class GlobalHotkeyConflictManager
    {
        private readonly Func<string, int> _sendIPCMessage;

        private static GlobalHotkeyConflictManager _instance;
        private AllHotkeyConflictsData _currentConflicts = new AllHotkeyConflictsData();

        public static GlobalHotkeyConflictManager Instance => _instance;

        public static void Initialize(Func<string, int> sendIPCMessage)
        {
            _instance = new GlobalHotkeyConflictManager(sendIPCMessage);
        }

        private GlobalHotkeyConflictManager(Func<string, int> sendIPCMessage)
        {
            _sendIPCMessage = sendIPCMessage;

            IPCResponseService.AllHotkeyConflictsReceived += OnAllHotkeyConflictsReceived;
        }

        public event EventHandler<AllHotkeyConflictsEventArgs> ConflictsUpdated;

        public void RequestAllConflicts()
        {
            var requestMessage = "{\"get_all_hotkey_conflicts\":{}}";
            _sendIPCMessage?.Invoke(requestMessage);
        }

        private void OnAllHotkeyConflictsReceived(object sender, AllHotkeyConflictsEventArgs e)
        {
            _currentConflicts = e.Conflicts;
            ConflictsUpdated?.Invoke(this, e);
        }

        public bool HasConflictForHotkey(HotkeySettings hotkey, string moduleName, int hotkeyID)
        {
            if (hotkey == null)
            {
                return false;
            }

            var allConflictGroups = _currentConflicts.InAppConflicts.Concat(_currentConflicts.SystemConflicts);

            foreach (var group in allConflictGroups)
            {
                if (IsHotkeyMatch(hotkey, group.Hotkey))
                {
                    if (!string.IsNullOrEmpty(moduleName) && hotkeyID >= 0)
                    {
                        var selfModule = group.Modules.FirstOrDefault(m =>
                            m.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase) &&
                            m.HotkeyID == hotkeyID);

                        if (selfModule != null && group.Modules.Count == 1)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        public HotkeyConflictInfo GetConflictInfo(HotkeySettings hotkey)
        {
            if (hotkey == null)
            {
                return null;
            }

            var allConflictGroups = _currentConflicts.InAppConflicts.Concat(_currentConflicts.SystemConflicts);

            foreach (var group in allConflictGroups)
            {
                if (IsHotkeyMatch(hotkey, group.Hotkey))
                {
                    var conflictModules = group.Modules.Where(m => m != null).ToList();
                    if (conflictModules.Count != 0)
                    {
                        var firstModule = conflictModules.First();
                        return new HotkeyConflictInfo
                        {
                            IsSystemConflict = group.IsSystemConflict,
                            ConflictingModuleName = firstModule.ModuleName,
                            ConflictingHotkeyID = firstModule.HotkeyID,
                            AllConflictingModules = conflictModules.Select(m => $"{m.ModuleName}:{m.HotkeyID}").ToList(),
                        };
                    }
                }
            }

            return null;
        }

        private bool IsHotkeyMatch(HotkeySettings settings, HotkeyData data)
        {
            return settings.Win == data.Win &&
                   settings.Ctrl == data.Ctrl &&
                   settings.Shift == data.Shift &&
                   settings.Alt == data.Alt &&
                   settings.Code == data.Key;
        }
    }
}
