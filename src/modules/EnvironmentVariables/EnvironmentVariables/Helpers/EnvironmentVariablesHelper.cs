// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using EnvironmentVariables.Helpers.Win32;
using EnvironmentVariables.Models;
using ManagedCommon;
using Microsoft.Win32;

namespace EnvironmentVariables.Helpers
{
    internal sealed class EnvironmentVariablesHelper
    {
        internal static string GetBackupVariableName(Variable variable, string profileName)
        {
            return variable.Name + "_PowerToys_" + profileName;
        }

        internal static Variable GetExisting(string variableName)
        {
            var userVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User);

            foreach (DictionaryEntry variable in userVariables)
            {
                var key = variable.Key as string;
                if (key.Equals(variableName, StringComparison.OrdinalIgnoreCase))
                {
                    return new Variable(key, userVariables[key] as string, VariablesSetType.User);
                }
            }

            var systemVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);

            foreach (DictionaryEntry variable in systemVariables)
            {
                var key = variable.Key as string;
                if (key.Equals(variableName, StringComparison.OrdinalIgnoreCase))
                {
                    return new Variable(key, systemVariables[key] as string, VariablesSetType.System);
                }
            }

            return null;
        }

        private static RegistryKey OpenEnvironmentKeyIfExists(bool fromMachine, bool writable)
        {
            RegistryKey baseKey;
            string keyName;

            if (fromMachine)
            {
                baseKey = Registry.LocalMachine;
                keyName = @"System\CurrentControlSet\Control\Session Manager\Environment";
            }
            else
            {
                baseKey = Registry.CurrentUser;
                keyName = "Environment";
            }

            return baseKey.OpenSubKey(keyName, writable: writable);
        }

        private static void SetEnvironmentVariableFromRegistryWithoutNotify(string variable, string value, bool fromMachine)
        {
            const int MaxUserEnvVariableLength = 255; // User-wide env vars stored in the registry have names limited to 255 chars
            if (!fromMachine && variable.Length >= MaxUserEnvVariableLength)
            {
                Logger.LogError("Can't apply variable - name too long.");
                return;
            }

            using (RegistryKey environmentKey = OpenEnvironmentKeyIfExists(fromMachine, writable: true))
            {
                if (environmentKey != null)
                {
                    if (value == null)
                    {
                        environmentKey.DeleteValue(variable, throwOnMissingValue: false);
                    }
                    else
                    {
                        environmentKey.SetValue(variable, value);
                    }
                }
            }
        }

        internal static void NotifyEnvironmentChange()
        {
            unsafe
            {
                // send a WM_SETTINGCHANGE message to all windows
                fixed (char* lParam = "Environment")
                {
                    _ = NativeMethods.SendNotifyMessage(new IntPtr(NativeMethods.HWND_BROADCAST), NativeMethods.WindowMessage.WM_SETTINGSCHANGED, (IntPtr)0x12345, (IntPtr)lParam);
                }
            }
        }

        internal static void GetVariables(EnvironmentVariableTarget target, VariablesSet set)
        {
            var variables = Environment.GetEnvironmentVariables(target);
            var sortedList = new SortedList<string, Variable>();

            foreach (DictionaryEntry variable in variables)
            {
                string key = variable.Key as string;
                string value = variable.Value as string;

                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                Variable entry = new Variable(key, value, set.Type);
                sortedList.Add(key, entry);
            }

            set.Variables = new System.Collections.ObjectModel.ObservableCollection<Variable>(sortedList.Values);
        }

        internal static bool SetVariableWithoutNotify(Variable variable)
        {
            bool fromMachine = variable.ParentType switch
            {
                VariablesSetType.Profile => false,
                VariablesSetType.User => false,
                VariablesSetType.System => true,
                _ => throw new NotImplementedException(),
            };

            SetEnvironmentVariableFromRegistryWithoutNotify(variable.Name, variable.Values, fromMachine);

            return true;
        }

        internal static bool SetVariable(Variable variable)
        {
            bool fromMachine = variable.ParentType switch
            {
                VariablesSetType.Profile => false,
                VariablesSetType.User => false,
                VariablesSetType.System => true,
                _ => throw new NotImplementedException(),
            };

            SetEnvironmentVariableFromRegistryWithoutNotify(variable.Name, variable.Values, fromMachine);
            NotifyEnvironmentChange();

            return true;
        }

        internal static bool UnsetVariableWithoutNotify(Variable variable)
        {
            bool fromMachine = variable.ParentType switch
            {
                VariablesSetType.Profile => false,
                VariablesSetType.User => false,
                VariablesSetType.System => true,
                _ => throw new NotImplementedException(),
            };

            SetEnvironmentVariableFromRegistryWithoutNotify(variable.Name, null, fromMachine);

            return true;
        }

        internal static bool UnsetVariable(Variable variable)
        {
            bool fromMachine = variable.ParentType switch
            {
                VariablesSetType.Profile => false,
                VariablesSetType.User => false,
                VariablesSetType.System => true,
                _ => throw new NotImplementedException(),
            };

            SetEnvironmentVariableFromRegistryWithoutNotify(variable.Name, null, fromMachine);
            NotifyEnvironmentChange();

            return true;
        }
    }
}
