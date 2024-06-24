// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using EnvironmentVariablesUILib.Helpers.Win32;
using EnvironmentVariablesUILib.Models;
using Microsoft.Win32;

namespace EnvironmentVariablesUILib.Helpers
{
    internal sealed class EnvironmentVariablesHelper
    {
        internal static string GetBackupVariableName(Variable variable, string profileName)
        {
            return variable.Name + "_PowerToys_" + profileName;
        }

        internal static Variable GetExisting(string variableName)
        {
            DefaultVariablesSet userSet = new DefaultVariablesSet(Guid.NewGuid(), "tmpUser", VariablesSetType.User);
            GetVariables(EnvironmentVariableTarget.User, userSet);

            foreach (var variable in userSet.Variables)
            {
                if (variable.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase))
                {
                    return new Variable(variable.Name, variable.Values, VariablesSetType.User);
                }
            }

            DefaultVariablesSet systemSet = new DefaultVariablesSet(Guid.NewGuid(), "tmpSystem", VariablesSetType.System);
            GetVariables(EnvironmentVariableTarget.Machine, systemSet);

            foreach (var variable in systemSet.Variables)
            {
                if (variable.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase))
                {
                    return new Variable(variable.Name, variable.Values, VariablesSetType.System);
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

        // Code taken from https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Environment.Win32.cs
        // Set variables directly to registry instead of using Environment API - Environment.SetEnvironmentVariable() has 1 second timeout for SendNotifyMessage(WM_SETTINGSCHANGED).
        // When applying profile, this would take num_of_variables * 1s to propagate the changes. We do manually SendNotifyMessage with no timeout where needed.
        private static void SetEnvironmentVariableFromRegistryWithoutNotify(string variable, string value, bool fromMachine)
        {
            const int MaxUserEnvVariableLength = 255; // User-wide env vars stored in the registry have names limited to 255 chars
            if (!fromMachine && variable.Length >= MaxUserEnvVariableLength)
            {
                LoggerInstance.Logger.LogError("Can't apply variable - name too long.");
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
                        // If a variable contains %, we save it as a REG_EXPAND_SZ, which is the same behavior as the Windows default environment variables editor.
                        if (value.Contains('%'))
                        {
                            environmentKey.SetValue(variable, value, RegistryValueKind.ExpandString);
                        }
                        else
                        {
                            environmentKey.SetValue(variable, value, RegistryValueKind.String);
                        }
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

        // Code taken from https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Environment.Win32.cs
        // Reading variables from registry instead of using Environment API, because Environment API expands variables by default.
        internal static void GetVariables(EnvironmentVariableTarget target, VariablesSet set)
        {
            var sortedList = new SortedList<string, Variable>();

            bool fromMachine = target == EnvironmentVariableTarget.Machine ? true : false;

            using (RegistryKey environmentKey = OpenEnvironmentKeyIfExists(fromMachine, writable: false))
            {
                if (environmentKey != null)
                {
                    foreach (string name in environmentKey.GetValueNames())
                    {
                        string value = environmentKey.GetValue(name, string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames).ToString();
                        try
                        {
                            Variable entry = new Variable(name, value, set.Type);
                            sortedList.Add(name, entry);
                        }
                        catch (ArgumentException)
                        {
                            // Throw and catch intentionally to provide non-fatal notification about corrupted environment block
                        }
                    }
                }
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
