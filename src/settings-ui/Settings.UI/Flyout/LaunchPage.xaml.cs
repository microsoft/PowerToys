// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Flyout
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LaunchPage : Page
    {
        public LaunchPage()
        {
            this.InitializeComponent();
        }

        private void Options_Click(object sender, RoutedEventArgs e)
        {
            Button selectedButton = sender as Button;

            Frame selectedFrame = this.Parent as Frame;
        }
    }
}
