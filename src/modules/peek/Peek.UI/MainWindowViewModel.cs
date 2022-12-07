// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI
{
    using System;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.UI.Xaml;

    public partial class MainWindowViewModel : ObservableObject
    {
        private const int NavigationThrottleDelayMs = 100;

        public MainWindowViewModel()
        {
            navigationThrottleTimer.Tick += NavigationThrottleTimer_Tick;
            navigationThrottleTimer.Interval = TimeSpan.FromMilliseconds(NavigationThrottleDelayMs);
        }

        public void AttemptLeftNavigation()
        {
            if (navigationThrottleTimer.IsEnabled)
            {
                return;
            }

            navigationThrottleTimer.Start();
            fileQuery.UpdateCurrentItemIndex(fileQuery.CurrentItemIndex - 1);
        }

        public void AttemptRightNavigation()
        {
            if (navigationThrottleTimer.IsEnabled)
            {
                return;
            }

            navigationThrottleTimer.Start();
            fileQuery.UpdateCurrentItemIndex(fileQuery.CurrentItemIndex + 1);
        }

        private void NavigationThrottleTimer_Tick(object? sender, object e)
        {
            if (sender == null)
            {
                return;
            }

            ((DispatcherTimer)sender).Stop();
        }

        [ObservableProperty]
        private FileQuery fileQuery = new ();

        private DispatcherTimer navigationThrottleTimer = new ();
    }
}
