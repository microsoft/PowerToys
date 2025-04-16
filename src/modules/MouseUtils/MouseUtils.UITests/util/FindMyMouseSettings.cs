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
    public class FindMyMouseSettings
    {
        // Appearance settings
        public string OverlayOpacity { get; set; }

        public string Radius { get; set; }

        public string InitialZoom { get; set; }

        public string AnimationDuration { get; set; }

        // Color settings
        public string BackgroundColor { get; set; }

        public string SpotlightColor { get; set; }

        // Activation method settings
        public enum ActivationMethod
        {
            PressLeftControlTwice,
            PressRightControlTwice,
            ShakeMouse,
            CustomShortcut,
        }

        public ActivationMethod SelectedActivationMethod { get; set; }

        // Optional constructor to initialize properties
        public FindMyMouseSettings(
            string overlayOpacity = "",
            string radius = "",
            string initialZoom = "",
            string animationDuration = "",
            string backgroundColor = "",
            string spotlightColor = "")
        {
            OverlayOpacity = overlayOpacity;
            Radius = radius;
            InitialZoom = initialZoom;
            AnimationDuration = animationDuration;
            BackgroundColor = backgroundColor;
            SpotlightColor = spotlightColor;
            SelectedActivationMethod = ActivationMethod.PressLeftControlTwice; // Default value
        }
    }
}
