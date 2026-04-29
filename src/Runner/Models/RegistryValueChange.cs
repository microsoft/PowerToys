// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.Win32;

namespace RunnerV2.Models
{
    internal readonly struct RegistryValueChange
    {
        public RegistryValueChange()
        {
        }

        public required string KeyPath { get; init; }

        public required string? KeyName { get; init; }

        public bool Required { get; init; } = true;

        public required object Value { get; init; }

        public RegistryHive Scope { get; init; } = RegistryHive.CurrentUser;

        private static RegistryValueKind ValueTypeToRegistryValueKind(object value)
        {
            return value switch
            {
                int => RegistryValueKind.DWord,
                long => RegistryValueKind.QWord,
                string => RegistryValueKind.String,
                string[] => RegistryValueKind.MultiString,
                byte[] => RegistryValueKind.Binary,
                _ => throw new ArgumentException("Unsupported value type"),
            };
        }

        public readonly bool IsApplied
        {
            get
            {
                try
                {
                    using RegistryKey? key = RegistryKey.OpenBaseKey(Scope, RegistryView.Default).OpenSubKey(KeyPath, false);
                    return key != null && ValueTypeToRegistryValueKind(Value) == key.GetValueKind(KeyName) && Value.Equals(key.GetValue(KeyName));
                }
                catch (Exception e)
                {
                    Logger.LogError($"Testing if registry change \"{this}\" is applied failed.", e);
                    return false;
                }
            }
        }

        public readonly bool RequiresElevation
        {
            get => Scope == RegistryHive.LocalMachine;
        }

        public readonly bool Apply()
        {
            try
            {
                using RegistryKey? key = RegistryKey.OpenBaseKey(Scope, RegistryView.Default).CreateSubKey(KeyPath, true);
                if (key == null)
                {
                    Logger.LogError($"Applying registry change \"{this}\" failed because the registry key could not be created.");
                    return false;
                }

                key.SetValue(KeyName, Value, ValueTypeToRegistryValueKind(Value));
                return true;
            }
            catch (Exception e)
            {
                Logger.LogError($"Applying registry change \"{this}\" failed.", e);
                return false;
            }
        }

        public readonly bool UnApply()
        {
            try
            {
                using RegistryKey? key = RegistryKey.OpenBaseKey(Scope, RegistryView.Default).OpenSubKey(KeyPath, true);
                if (key == null)
                {
                    Logger.LogError($"Unapplying registry change \"{this}\" failed because the registry key could not be opened.");
                    return false;
                }

                if (KeyName is not null)
                {
                    key.DeleteValue(KeyName, false);
                }
                else
                {
                    key.SetValue(null, string.Empty); // Delete the default value
                }

                // Check if the path doesn't contain anything and delete it if so
                if (key.GetValueNames().Length == 0 && key.GetSubKeyNames().Length == 0)
                {
                    RegistryKey.OpenBaseKey(Scope, RegistryView.Default).DeleteSubKey(KeyPath, false);
                }

                return true;
            }
            catch (Exception e)
            {
                Logger.LogError($"Unapplying registry change \"{this}\" failed.", e);
                return false;
            }
        }

        public override readonly string ToString() => $"{RegistryKey.OpenBaseKey(Scope, RegistryView.Default).Name}\\{KeyPath}\\{KeyName}:{Value}";
    }
}
