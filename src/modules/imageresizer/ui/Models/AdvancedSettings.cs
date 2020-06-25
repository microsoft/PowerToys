// This class sets the visibility property of Advanced settings based on the OS Version

using System;
using System.Runtime.InteropServices;

namespace ImageResizer.Models
{
    public class AdvancedSettings
    {
        [DllImport("../../os-detection.dll", EntryPoint = "UseNewSettings", ExactSpelling = false)]
        public static extern bool UseNewSettings();
    }
}
