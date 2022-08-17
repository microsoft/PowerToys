// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FancyZonesEditor.Controls
{
    using System.Windows;
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
            return Properties.Resources.Distance_adjacent_zones_slider_announce;
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Custom;
        }
    }
}
