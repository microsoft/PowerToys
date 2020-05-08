// This class sets the visibility property of Advanced settings based on the OS Version

using System;
using System.Runtime.InteropServices;

namespace ImageResizer.Models
{
    public class AdvancedSettings
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetDllDirectory(string lpPathName);

        [DllImport("os-detection.dll", EntryPoint = "UseNewSettings", ExactSpelling = false)]
        public static extern bool UseNewSettings();
    }
}
