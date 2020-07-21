// This class sets the visibility property of Advanced settings based on the OS Version

using System;
using System.Runtime.InteropServices;

namespace ImageResizer.Models
{
    public class AdvancedSettings
    {
        public static bool UseNewSettings()
        {
            return interop.CommonManaged.ShouldNewSettingsBeUsed();
        }
    }
}
