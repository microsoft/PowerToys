// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.SettingsXAML.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SearchPage : Page
    {
        public SearchPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is string searchQuery)
            {
                SearchTxt.Text = searchQuery;
            }
        }

        private void FilterBar_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            FilterBar.SelectedItem = FilterBar.Items[0];
        }
    }
}
