using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Plugin.Indexer
{
    public interface IRegistryWrapper
    {
        int GetHKLMRegistryValue(string registryLocation, string valueName);
    }
}
