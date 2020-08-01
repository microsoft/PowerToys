using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using Microsoft.Plugin.Uri.Interface;

namespace Microsoft.Plugin.Uri
{
    public class RegisteryWrapper : IRegistryWrapper
    {
        public string GetRegistryValue(string registryLocation, string valueName)
        {
            return Registry.GetValue(registryLocation, valueName, null) as string;
        }
    }
}
