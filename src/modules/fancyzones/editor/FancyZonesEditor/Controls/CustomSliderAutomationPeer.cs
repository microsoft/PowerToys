// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;

using FancyZonesEditor.Helpers;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace FancyZonesEditor.Controls
{
    internal sealed partial class CustomSliderAutomationPeer : SliderAutomationPeer
    {
        private static CompositeFormat _customSliderAnnounce;

        // ResourceLoader is not available at class-load time in every host, so this is resolved lazily.
        private static CompositeFormat CustomSliderAnnounce => _customSliderAnnounce ??=
            CompositeFormat.Parse(ResourceLoaderInstance.GetString("Custom_slider_announce"));

        private readonly string _name;

        public CustomSliderAutomationPeer(Slider owner)
            : base(owner)
        {
            _name = GetHelpText();
        }

        protected override string GetNameCore()
        {
            if (Owner is not Slider element)
            {
                return string.Empty;
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                CustomSliderAnnounce,
                _name,
                element.Minimum,
                element.Maximum,
                element.Value);
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Custom;
        }
    }
}
