// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace FancyZonesEditor.Controls
{
    /// <summary>
    /// A <see cref="Slider"/> that announces its label together with its range and current
    /// value, so screen reader users hear the full context on every change.
    /// </summary>
    public partial class CustomSlider : Slider
    {
        public CustomSlider()
        {
            // Reuse the built-in Slider template: a derived control has no implicit style
            // of its own in WinUI 3 and would otherwise render without a template.
            DefaultStyleKey = typeof(Slider);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new CustomSliderAutomationPeer(this);
        }
    }
}
