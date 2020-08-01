using Microsoft.Plugin.Uri.Interfaces;
using Microsoft.Win32;

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
