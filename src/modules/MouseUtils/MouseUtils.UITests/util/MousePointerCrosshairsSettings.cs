// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core.AnimationMetrics;

namespace MouseUtils.UITests
{
    public class MousePointerCrosshairsSettings
    {
        // Appearance settings
        public string Opacity { get; set; }

        public string CenterRadius { get; set; }

        public string Thickness { get; set; }

        public string BorderSize { get; set; }

        public bool IsFixLength { get; set; }

        public string FixedLength { get; set; }

        // Color settings
        public string CrosshairsColor { get; set; }

        public string CrosshairsBorderColor { get; set; }

        // Settings UI Elements
        public enum SettingsUIElements
        {
            CrosshairsColorGroup,
            CrosshairsBorderColorGroup,
            OpacitySlider,
            CenterRadiusEdit,
            ThicknessEdit,
            BorderSizeEdit,
            FixedLengthEdit,
            IsFixLengthToggle,
        }

        private Dictionary<SettingsUIElements, string> ElementNameMap { get; }

        // Optional constructor to initialize properties
        public MousePointerCrosshairsSettings(
            string opacity = "",
            string centerRadius = "",
            string thickness = "",
            string borderSize = "",
            bool isFixLength = false,
            string fixedLength = "",
            string crosshairsColor = "",
            string crosshairsBorderColor = "")
        {
            this.Opacity = opacity;
            this.CenterRadius = centerRadius;
            this.Thickness = thickness;
            this.BorderSize = borderSize;
            this.IsFixLength = isFixLength;
            this.FixedLength = fixedLength;
            this.CrosshairsColor = crosshairsColor;
            this.CrosshairsBorderColor = crosshairsBorderColor;
            ElementNameMap = new Dictionary<SettingsUIElements, string>
            {
                [SettingsUIElements.CrosshairsColorGroup] = @"Crosshairs color",
                [SettingsUIElements.CrosshairsBorderColorGroup] = @"Crosshairs border color",
                [SettingsUIElements.OpacitySlider] = @"Crosshairs opacity (%)",
                [SettingsUIElements.CenterRadiusEdit] = @"Crosshairs center radius (px) Minimum0 Maximum500",
                [SettingsUIElements.ThicknessEdit] = @"Crosshairs thickness (px) Minimum1 Maximum50",
                [SettingsUIElements.BorderSizeEdit] = @"Crosshairs border size (px) Minimum0 Maximum50",
                [SettingsUIElements.FixedLengthEdit] = @"Crosshairs fixed length (px) Minimum1",
                [SettingsUIElements.IsFixLengthToggle] = @"Fix crosshairs length",
            };
        }

        public string GetElementName(SettingsUIElements element)
        {
            return ElementNameMap[element];
        }
    }
}
