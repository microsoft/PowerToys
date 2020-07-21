using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Plugin.Indexer.Interface
{
    public interface IRegistryWrapper
    {
        int GetHKLMRegistryValue(string registryLocation, string valueName);
    }
}
