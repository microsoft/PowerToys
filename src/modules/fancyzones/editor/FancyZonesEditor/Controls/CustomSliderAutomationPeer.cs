// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace FancyZonesEditor.Controls
{
    internal class CustomSliderAutomationPeer : SliderAutomationPeer
    {
        private string name = string.Empty;

        public CustomSliderAutomationPeer(Slider owner)
            : base(owner)
        {
            name = GetHelpText();
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
                Properties.Resources.Custom_slider_announce,
                name,
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
