using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using Microsoft.Plugin.Indexer.Interface;
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
                if(regKey != null)
                {
                    Object value = regKey.GetValue(valueName);
                    if(value != null)
                    {
                        return (int)value;
                    }
                }
            }
            return 0;
        }
    }
}
