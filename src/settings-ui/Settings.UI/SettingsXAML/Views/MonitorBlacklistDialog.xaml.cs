// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Bisection stage 2 — full list-mode content restored, form-mode still removed.
    /// Click handlers are stubs (no-op); we only need to verify the dialog opens.
    /// </summary>
    public sealed partial class MonitorBlacklistDialog : ContentDialog
    {
        public PowerDisplayViewModel ViewModel { get; }

        public MonitorBlacklistDialog(PowerDisplayViewModel viewModel)
        {
            ViewModel = viewModel;
            this.InitializeComponent();
            Title = "Monitor blacklist (diagnostic)";
            CloseButtonText = "Close";

            UpdateCustomEmptyHintVisibility();
            ViewModel.DisplayedCustomBlacklist.CollectionChanged += (s, e) => UpdateCustomEmptyHintVisibility();
        }

        private void UpdateCustomEmptyHintVisibility()
        {
            CustomEmptyHint.Visibility = ViewModel.DisplayedCustomBlacklist.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void AddEntry_Click(object sender, RoutedEventArgs e)
        {
            // Diagnostic stub — no-op until dialog open is verified.
        }

        private void EditEntry_Click(object sender, RoutedEventArgs e)
        {
            // Diagnostic stub.
        }

        private void DeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            // Diagnostic stub.
        }
    }
}
