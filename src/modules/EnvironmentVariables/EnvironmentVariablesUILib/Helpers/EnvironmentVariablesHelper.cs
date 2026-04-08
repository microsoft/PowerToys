// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using EnvironmentVariablesUILib.Helpers.Win32;
using EnvironmentVariablesUILib.Models;
using Microsoft.Win32;

namespace EnvironmentVariablesUILib.Helpers
{
    internal sealed class EnvironmentVariablesHelper
    {
        // The Windows Environment Variables Editor and Regedit limit variable names to
        // 260 characters, including the terminating null character.
        private const int MaxEnvironmentVariableNameAuthoringLength = 259;

        // The maximum total length of an environment variable (name + '=' + value) is
        // 32767 characters, including the terminating null character.
        private const int MaxTotalEnvironmentVariableLength = 32766;

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

        internal static bool TryValidateVariableName(string variableName, out string errorMessage)
        {
            return TryValidateEnvironmentStyleName(variableName, out errorMessage);
        }

        internal static bool TryValidateProfileName(string profileName, out string errorMessage)
        {
            return TryValidateEnvironmentStyleName(profileName, out errorMessage);
        }

        /// <summary>
        /// Validates a backup variable name and value. Delegates to <see cref="TryValidateVariable"/>
        /// with authoring limits disabled; the only applicable length constraint is the
        /// 32767-character total budget for name + '=' + value + '\0'.
        /// </summary>
        internal static bool TryValidateBackupVariable(string backupName, string value, out string errorMessage)
        {
            return TryValidateVariable(backupName, value, out errorMessage, enforceAuthoringLimits: false);
        }

        internal static bool TryValidateVariableValue(string value, out string errorMessage)
        {
            if (value is not null && value.Contains('\0'))
            {
                errorMessage = "Environment variable value contains a null character.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        internal static bool TryValidateVariable(string name, string value, out string errorMessage, bool enforceAuthoringLimits = true)
        {
            if (!TryValidateEnvironmentStyleName(name, out errorMessage, enforceAuthoringLimits))
            {
                return false;
            }

            if (!TryValidateVariableValue(value, out errorMessage))
            {
                return false;
            }

            int totalLength = name.Length + 1 + (value?.Length ?? 0);
            if (totalLength > MaxTotalEnvironmentVariableLength)
            {
                errorMessage = $"The total length of the environment variable exceeds {MaxTotalEnvironmentVariableLength} characters.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static bool TryValidateEnvironmentStyleName(string name, out string errorMessage, bool enforceAuthoringLengthLimit = true)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                errorMessage = "Name is empty or whitespace.";
                return false;
            }

            if (!string.Equals(name, name.Trim(), StringComparison.Ordinal))
            {
                errorMessage = "Name cannot start or end with whitespace.";
                return false;
            }

            if (name.Contains('='))
            {
                errorMessage = "Name cannot contain '='.";
                return false;
            }

            foreach (char c in name)
            {
                if (char.IsControl(c))
                {
                    errorMessage = "Name cannot contain control characters.";
                    return false;
                }
            }

            if (enforceAuthoringLengthLimit && name.Length > MaxEnvironmentVariableNameAuthoringLength)
            {
                errorMessage = $"Name cannot exceed {MaxEnvironmentVariableNameAuthoringLength} characters.";
                return false;
            }

            errorMessage = null;
            return true;
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
        private static bool SetEnvironmentVariableFromRegistryWithoutNotify(string variable, string value, bool fromMachine, bool enforceAuthoringLimits = true)
        {
            if (!TryValidateVariable(variable, value, out string errorMessage, enforceAuthoringLimits))
            {
                LoggerInstance.Logger.LogError(
                    $"Can't apply variable '{variable}': {errorMessage}");
                return false;
            }

            try
            {
                using (RegistryKey environmentKey = OpenEnvironmentKeyIfExists(fromMachine, writable: true))
                {
                    if (environmentKey == null)
                    {
                        LoggerInstance.Logger.LogError("Failed to open environment registry key.");
                        return false;
                    }

                    if (value == null)
                    {
                        environmentKey.DeleteValue(variable, throwOnMissingValue: false);
                    }
                    else if (value.Contains('%'))
                    {
                        // If a variable contains %, we save it as a REG_EXPAND_SZ, which is the same behavior as the Windows default environment variables editor.
                        environmentKey.SetValue(variable, value, RegistryValueKind.ExpandString);
                    }
                    else
                    {
                        environmentKey.SetValue(variable, value, RegistryValueKind.String);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggerInstance.Logger.LogError($"Failed to write environment variable '{variable}'.", ex);
                return false;
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

            bool fromMachine = target == EnvironmentVariableTarget.Machine;

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

            return SetEnvironmentVariableFromRegistryWithoutNotify(variable.Name, variable.Values, fromMachine);
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

            bool success = SetEnvironmentVariableFromRegistryWithoutNotify(variable.Name, variable.Values, fromMachine);
            if (success)
            {
                NotifyEnvironmentChange();
            }

            return success;
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

            return SetEnvironmentVariableFromRegistryWithoutNotify(variable.Name, null, fromMachine);
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

            bool success = SetEnvironmentVariableFromRegistryWithoutNotify(variable.Name, null, fromMachine);
            if (success)
            {
                NotifyEnvironmentChange();
            }

            return success;
        }

        // Backup variables are always in user scope and exempt from the 259-character
        // authoring limit, since they are PowerToys-internal and never edited via Regedit.
        internal static bool SetBackupVariableWithoutNotify(Variable backupVariable)
        {
            return SetEnvironmentVariableFromRegistryWithoutNotify(
                backupVariable.Name, backupVariable.Values, fromMachine: false, enforceAuthoringLimits: false);
        }

        internal static bool UnsetBackupVariableWithoutNotify(Variable backupVariable)
        {
            return SetEnvironmentVariableFromRegistryWithoutNotify(
                backupVariable.Name, null, fromMachine: false, enforceAuthoringLimits: false);
        }
    }
}
