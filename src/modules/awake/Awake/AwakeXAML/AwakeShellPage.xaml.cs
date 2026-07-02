// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using Awake.ViewModels;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace Awake
{
    /// <summary>
    /// Hosts the flyout's navigation frame. The launch page is the root; the custom-time and
    /// app-picker pages slide in over it with a back button (same pattern as the QuickAccess
    /// flyout shell).
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)]
    public sealed partial class AwakeShellPage : Page
    {
        private AwakeFlyoutNavigationContext? _context;

        public AwakeShellPage()
        {
            InitializeComponent();
            ContentFrame.NavigationFailed += OnNavigationFailed;
        }

        /// <summary>
        /// Raised when a hosted page asks the window to dismiss the flyout (Escape on the launch
        /// page, or opening Settings).
        /// </summary>
        public event EventHandler? CloseRequested;

        public void Initialize(AwakeFlyoutViewModel viewModel)
        {
            _context = new AwakeFlyoutNavigationContext(
                viewModel,
                () => CloseRequested?.Invoke(this, EventArgs.Empty));
        }

        /// <summary>
        /// Resets the frame to the launch page (clearing any sub-page and the back stack) so each
        /// time the flyout is summoned it opens on the main view. A fresh navigation also rebuilds
        /// the header glow with the current accent color.
        /// </summary>
        public void NavigateToLaunch()
        {
            if (_context == null)
            {
                return;
            }

            ContentFrame.Navigate(typeof(AwakeLaunchPage), _context, new SuppressNavigationTransitionInfo());
            ContentFrame.BackStack.Clear();
        }

        public void FocusContent()
        {
            if (ContentFrame.Content is AwakeLaunchPage launchPage)
            {
                launchPage.FocusContent();
            }
            else
            {
                (ContentFrame.Content as Control)?.Focus(FocusState.Programmatic);
            }
        }

        /// <summary>
        /// Forwards a glow rebuild to the launch page when it is the active content.
        /// </summary>
        public void RefreshGlow()
        {
            if (ContentFrame.Content is AwakeLaunchPage launchPage)
            {
                launchPage.RefreshGlow();
            }
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            if (_context == null || ContentFrame.Content is AwakeLaunchPage)
            {
                return;
            }

            ContentFrame.Navigate(typeof(AwakeLaunchPage), _context, new SuppressNavigationTransitionInfo());
        }

        private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            // A page constructor or XAML load failure here would otherwise crash the flyout.
            // Log and mark handled so the flyout stays available; the next summon retries.
            Logger.LogError($"Awake: navigation to '{e.SourcePageType?.FullName}' failed.", e.Exception);
            e.Handled = true;
        }
    }
}
