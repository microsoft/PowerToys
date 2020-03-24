using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace Wox.Plugin.Indexer.SearchHelper
{
    class IndexerExecutableInfo
    {
        // Reference - Control Panel Plugin
        private const string CONTROL = @"%SystemRoot%\System32\control.exe";
        private RegistryKey nameSpace = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\ControlPanel\\NameSpace");
        RegistryKey clsid = Registry.ClassesRoot.OpenSubKey("CLSID");
        RegistryKey currentKey;
        ProcessStartInfo executablePath;

        public ProcessStartInfo Create(uint iconSize)
        {
            foreach (string key in nameSpace.GetSubKeyNames())
            {
                currentKey = clsid.OpenSubKey(key);
                if (currentKey != null)
                {
                    executablePath = getExecutablePath(currentKey);

                    if (!(executablePath == null)) //Cannot have item without executable path
                    {
                        localizedString = getLocalizedString(currentKey);
                    }
                }
            }

            return executablePath;
        }


        // Ref - ControlPanelPlugin Wox
        // Code to obtain the executable path for an item in the Control Panel
        private ProcessStartInfo getExecutablePath(RegistryKey currentKey)
        {
            ProcessStartInfo executablePath = new ProcessStartInfo();
            string applicationName;

            if (currentKey.GetValue("System.ApplicationName") != null)
            {
                //CPL Files (usually native MS items)
                applicationName = currentKey.GetValue("System.ApplicationName").ToString();
                executablePath.FileName = Environment.ExpandEnvironmentVariables(CONTROL);
                executablePath.Arguments = "-name " + applicationName;
            }
            else if (currentKey.OpenSubKey("Shell\\Open\\Command") != null && currentKey.OpenSubKey("Shell\\Open\\Command").GetValue(null) != null)
            {
                //Other files (usually third party items)
                string input = "\"" + Environment.ExpandEnvironmentVariables(currentKey.OpenSubKey("Shell\\Open\\Command").GetValue(null).ToString()) + "\"";
                executablePath.FileName = "cmd.exe";
                executablePath.Arguments = "/C " + input;
                executablePath.WindowStyle = ProcessWindowStyle.Hidden;
            }
            else
            {
                return null;
            }

            return executablePath;
        }
    }
}
