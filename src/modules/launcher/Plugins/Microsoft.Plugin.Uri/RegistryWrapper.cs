// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Plugin.Uri.Interfaces;
using Microsoft.Win32;

namespace Microsoft.Plugin.Uri
{
    public class RegistryWrapper : IRegistryWrapper
    {
        public string GetRegistryValue(string registryLocation, string valueName)
        {
            return Registry.GetValue(registryLocation, valueName, null) as string;
        }
    }
}
