// This class sets the visibility property of Advanced settings based on the OS Version

namespace ImageResizer.Models
{
    public static class AdvancedSettings
    {
        public static bool UseNewSettings()
        {
            return interop.CommonManaged.ShouldNewSettingsBeUsed();
        }
    }
}
