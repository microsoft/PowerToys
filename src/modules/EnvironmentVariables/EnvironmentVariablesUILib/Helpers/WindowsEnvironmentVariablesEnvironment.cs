// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable annotations

using System;

using EnvironmentVariablesUILib.Helpers.Win32;
using Microsoft.Win32;

namespace EnvironmentVariablesUILib.Helpers
{
    internal sealed class WindowsEnvironmentVariablesEnvironment : IEnvironmentVariablesEnvironment
    {
        internal static WindowsEnvironmentVariablesEnvironment Instance { get; } = new();

        public IEnvironmentVariablesRegistryKey? OpenKey(RegistryHive hive, string subKey, bool writable)
        {
            var baseKey = hive switch
            {
                RegistryHive.CurrentUser => Registry.CurrentUser,
                RegistryHive.LocalMachine => Registry.LocalMachine,
                _ => throw new NotImplementedException(),
            };
            var key = baseKey.OpenSubKey(subKey, writable: writable);
            return key == null ? null : new RegistryKeyHandle(key);
        }

        public string ExpandEnvironmentVariables(string value)
        {
            return Environment.ExpandEnvironmentVariables(value);
        }

        public int SendNotifyMessage(IntPtr window, int message, IntPtr wParam, string lParam)
        {
            unsafe
            {
                fixed (char* text = lParam)
                {
                    return NativeMethods.SendNotifyMessage(window, (NativeMethods.WindowMessage)message, wParam, (IntPtr)text);
                }
            }
        }

        private sealed class RegistryKeyHandle : IEnvironmentVariablesRegistryKey
        {
            private readonly RegistryKey key;

            internal RegistryKeyHandle(RegistryKey key)
            {
                this.key = key;
            }

            public string[] GetValueNames()
            {
                return key.GetValueNames();
            }

            public object? GetValue(string name, object defaultValue, RegistryValueOptions options)
            {
                return key.GetValue(name, defaultValue, options);
            }

            public void SetValue(string name, object value, RegistryValueKind kind)
            {
                key.SetValue(name, value, kind);
            }

            public void DeleteValue(string name, bool throwOnMissingValue)
            {
                key.DeleteValue(name, throwOnMissingValue);
            }

            public void Dispose()
            {
                key.Dispose();
            }
        }
    }
}
