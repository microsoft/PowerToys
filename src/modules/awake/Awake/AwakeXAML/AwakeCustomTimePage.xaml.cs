// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Awake.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace Awake
{
    /// <summary>
    /// Lets the user configure a custom keep-awake duration (hours/minutes) or an "until a
    /// specific date and time" expiration, then applies it and returns to the launch page.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)]
    public sealed partial class AwakeCustomTimePage : Page
    {
        private AwakeFlyoutNavigationContext? _context;

        public AwakeCustomTimePage()
        {
            InitializeComponent();
        }

        public AwakeFlyoutViewModel ViewModel { get; private set; } = default!;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is AwakeFlyoutNavigationContext context)
            {
                _context = context;
                ViewModel = context.ViewModel;
                this.Bindings.Update();

                // Reflect the current pending selection so reopening keeps the chosen sub-mode.
                // The SwitchPresenter swaps the duration/until panels off this selection in XAML.
                CustomTypeSelector.SelectedIndex = ViewModel.PendingCustomIsUntil ? 1 : 0;
            }
        }

        private void OnApplyClick(object sender, RoutedEventArgs e)
        {
            ViewModel.SetPendingCustom(CustomTypeSelector.SelectedIndex == 1);

            GoBack();
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => GoBack();

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                GoBack();
                e.Handled = true;
            }
        }

        private void GoBack()
        {
            if (Frame != null && Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}
