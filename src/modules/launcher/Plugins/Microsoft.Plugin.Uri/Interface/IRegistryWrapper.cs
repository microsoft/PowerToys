using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Plugin.Uri.Interface
{
    public interface IRegistryWrapper
    {
        string GetRegistryValue(string registryLocation, string valueName);
    }
}
