// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FancyZonesEditor.Controls
{
    using System.Windows.Automation.Peers;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for CustomSlider.xaml
    /// </summary>
    public partial class CustomSlider : Slider
    {
        public CustomSlider()
        {
            InitializeComponent();
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new CustomSliderAutomationPeer(this);
        }
    }
}
