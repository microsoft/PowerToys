namespace Microsoft.Plugin.Uri.Interfaces
{
    public interface IRegistryWrapper
    {
        string GetRegistryValue(string registryLocation, string valueName);
    }
}
