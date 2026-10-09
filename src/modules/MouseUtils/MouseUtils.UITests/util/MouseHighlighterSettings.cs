// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MouseUtils.UITests
{
    public class MouseHighlighterSettings
    {
        // Appearance settings
        public string Radius { get; set; }

        public string FadeDelay { get; set; }

        public string FadeDuration { get; set; }

        // Color settings
        public string PrimaryButtonHighlightColor { get; set; }

        public string SecondaryButtonHighlightColor { get; set; }

        public string AlwaysHighlightColor { get; set; }

        // Settings UI Elements
        public enum SettingsUIElements
        {
            PrimaryButtonHighlightColorGroup,
            SecondaryButtonHighlightColorGroup,
            AlwaysHighlightColorGroup,
            RadiusEdit,
            FadeDelayEdit,
            FadeDurationEdit,
        }

        private Dictionary<SettingsUIElements, string> ElementNameMap { get; }

        // Optional constructor to initialize properties
        public MouseHighlighterSettings(
            string radius = "",
            string fadeDelay = "",
            string fadeDuration = "",
            string primaryButtonHighlightColor = "",
            string secondaryButtonHighlightColor = "",
            string alwaysHighlightColor = "")
        {
            this.Radius = radius;
            this.FadeDelay = fadeDelay;
            this.FadeDuration = fadeDuration;
            this.PrimaryButtonHighlightColor = primaryButtonHighlightColor;
            this.SecondaryButtonHighlightColor = secondaryButtonHighlightColor;
            this.AlwaysHighlightColor = alwaysHighlightColor;

            ElementNameMap = new Dictionary<SettingsUIElements, string>
            {
                [SettingsUIElements.PrimaryButtonHighlightColorGroup] = @"Primary button highlight color",
                [SettingsUIElements.SecondaryButtonHighlightColorGroup] = @"Secondary button highlight color",
                [SettingsUIElements.AlwaysHighlightColorGroup] = @"Always highlight color",
                [SettingsUIElements.RadiusEdit] = @"Radius (px) Minimum5",
                [SettingsUIElements.FadeDelayEdit] = @"Fade delay (ms) Minimum0",
                [SettingsUIElements.FadeDurationEdit] = @"Fade duration (ms) Minimum0",
            };
        }

        public string GetElementName(SettingsUIElements element)
        {
            return ElementNameMap[element];
        }
    }
}
