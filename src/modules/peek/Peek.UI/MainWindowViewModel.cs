// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI
{
    using System;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.UI.Xaml;
    using Peek.UI.FileSystem;

    public partial class MainWindowViewModel : ObservableObject
    {
        private const int NavigationThrottleDelayMs = 100;

        [ObservableProperty]
        private FolderItemsQuery _folderItemsQuery = new ();

        public MainWindowViewModel()
        {
            NavigationThrottleTimer.Tick += NavigationThrottleTimer_Tick;
            NavigationThrottleTimer.Interval = TimeSpan.FromMilliseconds(NavigationThrottleDelayMs);
        }

        private DispatcherTimer NavigationThrottleTimer { get; set; } = new ();

        public void AttemptLeftNavigation()
        {
            if (NavigationThrottleTimer.IsEnabled)
            {
                return;
            }

            NavigationThrottleTimer.Start();

            // TODO: return a bool so UI can give feedback in case navigation is unavailable
            FolderItemsQuery.UpdateCurrentItemIndex(FolderItemsQuery.CurrentItemIndex - 1);
        }

        public void AttemptRightNavigation()
        {
            if (NavigationThrottleTimer.IsEnabled)
            {
                return;
            }

            NavigationThrottleTimer.Start();

            // TODO: return a bool so UI can give feedback in case navigation is unavailable
            FolderItemsQuery.UpdateCurrentItemIndex(FolderItemsQuery.CurrentItemIndex + 1);
        }

        private void NavigationThrottleTimer_Tick(object? sender, object e)
        {
            if (sender == null)
            {
                return;
            }

            ((DispatcherTimer)sender).Stop();
        }
    }
}
