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

        private bool IsNavigating { get; set; } = false;

        public MainWindowViewModel()
        {
            NavigationThrottleTimer.Tick += NavigationThrottleTimer_Tick;
            NavigationThrottleTimer.Interval = TimeSpan.FromMilliseconds(NavigationThrottleDelayMs);
        }

        private async void AttemptNavigation(bool goToNextItem)
        {
            if (NavigationThrottleTimer.IsEnabled && !IsNavigating)
            {
                return;
            }

            IsNavigating = true;
            NavigationThrottleTimer.Start();

            var desiredItemIndex = FolderItemsQuery.CurrentItemIndex + (goToNextItem ? 1 : -1);

            // TODO: return a bool so UI can give feedback in case navigation is unavailable?
            await FolderItemsQuery.UpdateCurrentItemIndex(desiredItemIndex);
            IsNavigating = false;
        }

        public void AttemptLeftNavigation()
        {
            AttemptNavigation(false);
        }

        public void AttemptRightNavigation()
        {
            AttemptNavigation(true);
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
        private FolderItemsQuery _folderItemsQuery = new ();

        private DispatcherTimer NavigationThrottleTimer { get; set; } = new ();
    }
}
