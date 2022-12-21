// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32;

namespace Microsoft.Plugin.Indexer.DriveDetection
{
    public class RegistryWrapper : IRegistryWrapper
    {
        // Given the registrypath and the name of the value, to retrieve the data corresponding to that registry key
        public int GetHKLMRegistryValue(string registryLocation, string valueName)
        {
            using (RegistryKey regKey = Registry.LocalMachine.OpenSubKey(registryLocation))
            {
                if (regKey != null)
                {
                    object value = regKey.GetValue(valueName);
                    if (value != null)
                    {
                        return (int)value;
                    }
                }
            }

            return 0;
        }
    }
}
