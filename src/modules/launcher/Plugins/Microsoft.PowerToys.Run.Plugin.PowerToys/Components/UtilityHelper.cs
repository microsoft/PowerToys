// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Common.UI;
using interop;
using Wox.Plugin;

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
                _ => null,
            };
        }

        public static Func<ActionContext, bool> GetAction(UtilityKey key)
        {
            return (context) =>
            {
                switch (key)
                {
                    case UtilityKey.ColorPicker: // Launch ColorPicker
                        using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowColorPickerSharedEvent()))
                        {
                            eventHandle.Set();
                        }

                        break;
                    case UtilityKey.FancyZones: // Launch FancyZones Editor
                        using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.FZEToggleEvent()))
                        {
                            eventHandle.Set();
                        }

                        break;

                    case UtilityKey.Hosts: // Launch Hosts
                        {
                            using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowHostsSharedEvent()))
                            {
                                eventHandle.Set();
                            }
                        }

                        break;

                    case UtilityKey.MeasureTool: // Launch Screen Ruler
                        using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.MeasureToolTriggerEvent()))
                        {
                            eventHandle.Set();
                        }

                        break;
                    case UtilityKey.PowerOCR: // Launch Text Extractor
                        using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowPowerOCRSharedEvent()))
                        {
                            eventHandle.Set();
                        }

                        break;

                    case UtilityKey.ShortcutGuide: // Launch Shortcut Guide
                        using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShortcutGuideTriggerEvent()))
                        {
                            eventHandle.Set();
                        }

                        break;

                    default:
                        break;
                }

                return true;
            };
        }
    }
}
