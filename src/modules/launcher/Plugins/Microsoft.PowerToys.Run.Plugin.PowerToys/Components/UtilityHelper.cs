// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Common.UI;

namespace Microsoft.PowerToys.Run.Plugin.PowerToys.Components
{
    public static class UtilityHelper
    {
        public static string GetIcoPath(UtilityKey key)
        {
            return key switch
            {
                UtilityKey.ColorPicker => "Images/ColorPicker.png",
                UtilityKey.FancyZones => "Images/FancyZones.png",
                UtilityKey.Hosts => "Images/Hosts.png",
                UtilityKey.MeasureTool => "Images/ScreenRuler.png",
                UtilityKey.PowerOCR => "Images/PowerOcr.png",
                UtilityKey.ShortcutGuide => "Images/ShortcutGuide.png",
                UtilityKey.RegistryPreview => "Images/RegistryPreview.png",
                UtilityKey.CropAndLock => "Images/CropAndLock.png",
                UtilityKey.EnvironmentVariables => "Images/EnvironmentVariables.png",
                _ => null,
            };
        }

        public static SettingsDeepLink.SettingsWindow? GetSettingsWindow(UtilityKey key)
        {
            return key switch
            {
                UtilityKey.ColorPicker => SettingsDeepLink.SettingsWindow.ColorPicker,
                UtilityKey.FancyZones => SettingsDeepLink.SettingsWindow.FancyZones,
                UtilityKey.Hosts => SettingsDeepLink.SettingsWindow.Hosts,
                UtilityKey.MeasureTool => SettingsDeepLink.SettingsWindow.MeasureTool,
                UtilityKey.PowerOCR => SettingsDeepLink.SettingsWindow.PowerOCR,
                UtilityKey.ShortcutGuide => SettingsDeepLink.SettingsWindow.ShortcutGuide,
                UtilityKey.RegistryPreview => SettingsDeepLink.SettingsWindow.RegistryPreview,
                UtilityKey.CropAndLock => SettingsDeepLink.SettingsWindow.CropAndLock,
                UtilityKey.EnvironmentVariables => SettingsDeepLink.SettingsWindow.EnvironmentVariables,
                _ => null,
            };
        }
    }
}
