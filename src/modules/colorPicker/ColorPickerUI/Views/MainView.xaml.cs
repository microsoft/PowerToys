// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace ColorPicker.Views
{
    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public sealed partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void EnableNarratorColorChangesAnnouncements()
        {
            if (DataContext is not INotifyPropertyChanged viewModel)
            {
                return;
            }

            viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName != "ColorName")
                {
                    return;
                }

                if (ColorTextBlock == null)
                {
                    return;
                }

                // WinUI has no AutomationEvents.MenuOpened narrator-announcement hack; with the
                // TextBlock marked AutomationProperties.LiveSetting="Assertive", raising
                // LiveRegionChanged is the idiomatic way to announce the new color.
                var peer = FrameworkElementAutomationPeer.FromElement(ColorTextBlock)
                           ?? FrameworkElementAutomationPeer.CreatePeerForElement(ColorTextBlock);

                peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnableNarratorColorChangesAnnouncements();
        }
    }
}
