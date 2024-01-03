// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;

namespace SvgPreviewHandler
{
    internal sealed class Settings
    {
        private static readonly SettingsUtils ModuleSettings = new SettingsUtils();

        public int ColorMode
        {
            get
            {
                try
                {
                    return ModuleSettings.GetSettings<PowerPreviewSettings>(PowerPreviewSettings.ModuleName).Properties.SvgBackgroundColorMode.Value;
                }
                catch (FileNotFoundException)
                {
                    return PowerPreviewProperties.DefaultSvgBackgroundColorMode;
                }
            }
        }

        public Color SolidColor
        {
            get
            {
                try
                {
                    var colorString = ModuleSettings.GetSettings<PowerPreviewSettings>(PowerPreviewSettings.ModuleName).Properties.SvgBackgroundSolidColor.Value;
                    return ColorTranslator.FromHtml(colorString);
                }
                catch (FileNotFoundException)
                {
                    return ColorTranslator.FromHtml(PowerPreviewProperties.DefaultSvgBackgroundSolidColor);
                }
            }
        }

        public Color ThemeColor
        {
            get
            {
                if (string.Equals(Common.UI.ThemeManager.GetWindowsBaseColor(), "dark", StringComparison.OrdinalIgnoreCase))
                {
                    return Color.FromArgb(30, 30, 30); // #1e1e1e
                }
                else
                {
                    return Color.White;
                }
            }
        }

        public int CheckeredShade
        {
            get
            {
                try
                {
                    return ModuleSettings.GetSettings<PowerPreviewSettings>(PowerPreviewSettings.ModuleName).Properties.SvgBackgroundCheckeredShade.Value;
                }
                catch (FileNotFoundException)
                {
                    return PowerPreviewProperties.DefaultSvgBackgroundCheckeredShade;
                }
            }
        }
    }
}
