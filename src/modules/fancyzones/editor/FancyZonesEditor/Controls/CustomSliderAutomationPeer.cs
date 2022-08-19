// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FancyZonesEditor.Controls
{
    using System.Globalization;
    using System.Windows.Automation.Peers;
    using System.Windows.Controls;

    internal class CustomSliderAutomationPeer : SliderAutomationPeer
    {
        public CustomSliderAutomationPeer(Slider owner)
            : base(owner)
        {
        }

        protected override string GetNameCore()
        {
            var element = this.Owner as Slider;
            if (element == null)
            {
                return string.Empty;
            }

            string announce = string.Format(
                CultureInfo.CurrentCulture,
                Properties.Resources.Distance_adjacent_zones_slider_announce,
                element.Minimum,
                element.Maximum,
                element.Value);

            return announce;
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Custom;
        }
    }
}
