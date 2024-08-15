// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.GPOWrapperProjection
{
    public enum GpoRuleConfigured : int
    {
        WrongValue = PowerToys.GPOWrapper.GpoRuleConfigured.WrongValue,
        Unavailable = PowerToys.GPOWrapper.GpoRuleConfigured.Unavailable,
        NotConfigured = PowerToys.GPOWrapper.GpoRuleConfigured.NotConfigured,
        Disabled = PowerToys.GPOWrapper.GpoRuleConfigured.Disabled,
        Enabled = PowerToys.GPOWrapper.GpoRuleConfigured.Enabled,
    }

    // Some WPF applications have trouble consuming WinRT/C++ projections, because WPF makes an intermediary _wpftmp.csproj which doesn't build projections correctly on MSBUILD.
    // This is a workaround to give access to GPOWrapper for WPF applications.
    public static class GPOWrapper
    {
        public static GpoRuleConfigured GetConfiguredPowerLauncherEnabledValue()
        {
            return (GpoRuleConfigured)PowerToys.GPOWrapper.GPOWrapper.GetConfiguredPowerLauncherEnabledValue();
        }

        public static GpoRuleConfigured GetConfiguredFancyZonesEnabledValue()
        {
            return (GpoRuleConfigured)PowerToys.GPOWrapper.GPOWrapper.GetConfiguredFancyZonesEnabledValue();
        }

        public static GpoRuleConfigured GetConfiguredCmdNotFoundEnabledValue()
        {
            return (GpoRuleConfigured)PowerToys.GPOWrapper.GPOWrapper.GetConfiguredCmdNotFoundEnabledValue();
        }

        public static GpoRuleConfigured GetConfiguredColorPickerEnabledValue()
        {
            return (GpoRuleConfigured)PowerToys.GPOWrapper.GPOWrapper.GetConfiguredColorPickerEnabledValue();
        }

        public static GpoRuleConfigured GetConfiguredImageResizerEnabledValue()
        {
            return (GpoRuleConfigured)PowerToys.GPOWrapper.GPOWrapper.GetConfiguredImageResizerEnabledValue();
        }

        public static GpoRuleConfigured GetConfiguredTextExtractorEnabledValue()
        {
            return (GpoRuleConfigured)PowerToys.GPOWrapper.GPOWrapper.GetConfiguredTextExtractorEnabledValue();
        }

        public static GpoRuleConfigured GetConfiguredAdvancedPasteEnabledValue()
        {
            return (GpoRuleConfigured)PowerToys.GPOWrapper.GPOWrapper.GetConfiguredAdvancedPasteEnabledValue();
        }

        public static GpoRuleConfigured GetConfiguredPeekEnabledValue()
        {
            return (GpoRuleConfigured)PowerToys.GPOWrapper.GPOWrapper.GetConfiguredPeekEnabledValue();
        }

        public static GpoRuleConfigured GetRunPluginEnabledValue(string pluginID)
        {
            return (GpoRuleConfigured)PowerToys.GPOWrapper.GPOWrapper.GetRunPluginEnabledValue(pluginID);
        }
    }
}
