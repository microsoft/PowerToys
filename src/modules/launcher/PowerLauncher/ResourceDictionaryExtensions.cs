using System.Collections.Generic;
using System.Windows;

namespace PowerLauncher
{
    public static class ResourceDictionaryExtensions
    {
        private static readonly Dictionary<ResourceDictionary, string> _mapping = new Dictionary<ResourceDictionary, string>();
        public static void SetName(ResourceDictionary element, string value)
        {
            _mapping[element] = value;
        }

        public static string GetName(ResourceDictionary element)
        {
            if (!_mapping.ContainsKey(element))
            {
                return null;
            }
            return _mapping[element];
        }
    }
}
