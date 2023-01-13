// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace ColorPicker.Views
{
    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public partial class MainView : UserControl
    {
        private void EnableNarratorColorChangesAnnouncements()
        {
            INotifyPropertyChanged viewModel = (INotifyPropertyChanged)this.DataContext;
            viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName != "ColorName")
                {
                    return;
                }

                var colorTextBlock = (TextBlock)FindName("ColorTextBlock");
                if (colorTextBlock == null)
                {
                    return;
                }

                var peer = UIElementAutomationPeer.FromElement(colorTextBlock);
                if (peer == null)
                {
                    peer = UIElementAutomationPeer.CreatePeerForElement(colorTextBlock);
                }

                peer.RaiseAutomationEvent(AutomationEvents.MenuOpened);
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnableNarratorColorChangesAnnouncements();
        }

        public MainView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }
    }
}
