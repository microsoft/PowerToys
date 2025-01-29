// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Win32;

namespace Microsoft.PowerToys.Telemetry
{
    public static class DataDiagnosticsSettings
    {
        private static readonly string DataDiagnosticsRegistryKey = @"HKEY_CURRENT_USER\Software\Classes\PowerToys\";
        private static readonly string DataDiagnosticsRegistryValueName = @"AllowDataDiagnostics";
        private static readonly string DataDiagnosticsDataDiagnosticsUserActionRegistryValueName = @"DataDiagnosticsUserAction";
        private static readonly string DataDiagnosticsDataDiagnosticsViewDataRegistryValueName = @"DataDiagnosticsViewEnabled";

        public static bool GetEnabledValue()
        {
            object registryValue = null;
            try
            {
                registryValue = Registry.GetValue(DataDiagnosticsRegistryKey, DataDiagnosticsRegistryValueName, 0);

                if (registryValue is not null)
                {
                    return (int)registryValue == 1 ? true : false;
                }
            }
            catch
            {
            }

            return false;
        }

        public static void SetEnabledValue(bool value)
        {
            try
            {
                Registry.SetValue(DataDiagnosticsRegistryKey, DataDiagnosticsRegistryValueName, value ? 1 : 0);
            }
            catch (Exception)
            {
            }
        }

        public static bool GetUserActionValue()
        {
            object registryValue = null;
            try
            {
                registryValue = Registry.GetValue(DataDiagnosticsRegistryKey, DataDiagnosticsDataDiagnosticsUserActionRegistryValueName, 0);
            }
            catch
            {
            }

            if (registryValue is not null)
            {
                return (int)registryValue == 1 ? true : false;
            }

            return false;
        }

        public static void SetUserActionValue(bool value)
        {
            try
            {
                Registry.SetValue(DataDiagnosticsRegistryKey, DataDiagnosticsDataDiagnosticsUserActionRegistryValueName, value ? 1 : 0);
            }
            catch (Exception)
            {
            }
        }

        public static bool GetViewEnabledValue()
        {
            object registryValue = null;
            try
            {
                registryValue = Registry.GetValue(DataDiagnosticsRegistryKey, DataDiagnosticsDataDiagnosticsViewDataRegistryValueName, 0);
            }
            catch
            {
            }

            if (registryValue is not null)
            {
                return (int)registryValue == 1 ? true : false;
            }

            return false;
        }

        public static void SetViewEnabledValue(bool value)
        {
            try
            {
                Registry.SetValue(DataDiagnosticsRegistryKey, DataDiagnosticsDataDiagnosticsViewDataRegistryValueName, value ? 1 : 0);
            }
            catch (Exception)
            {
            }
        }
    }
}
