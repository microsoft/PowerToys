// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.UI.Models;
using Windows.Win32.Foundation;

namespace Peek.UI
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private static readonly string _defaultWindowTitle = ResourceLoaderInstance.ResourceLoader.GetString("AppTitle/Title");
        private const int NavigationThrottleDelayMs = 100;

        [ObservableProperty]
        private int _currentIndex;

        [ObservableProperty]
        private IFileSystemItem? _currentItem;

        partial void OnCurrentItemChanged(IFileSystemItem? value)
        {
            WindowTitle = value != null
                ? ReadableStringHelper.FormatResourceString("WindowTitle", value.Name)
                : _defaultWindowTitle;
        }

        [ObservableProperty]
        private string _windowTitle;

        [ObservableProperty]
        private NeighboringItems? _items;

        [ObservableProperty]
        private double _scalingFactor = 1.0;

        public NeighboringItemsQuery NeighboringItemsQuery { get; }

        private DispatcherTimer NavigationThrottleTimer { get; set; } = new();

        public MainWindowViewModel(NeighboringItemsQuery query)
        {
            NeighboringItemsQuery = query;
            WindowTitle = _defaultWindowTitle;

            NavigationThrottleTimer.Tick += NavigationThrottleTimer_Tick;
            NavigationThrottleTimer.Interval = TimeSpan.FromMilliseconds(NavigationThrottleDelayMs);
        }

        public void Initialize(HWND foregroundWindowHandle)
        {
            try
            {
                Items = NeighboringItemsQuery.GetNeighboringItems(foregroundWindowHandle);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to get File Explorer Items: " + ex.Message);
            }

            CurrentIndex = 0;

            if (Items != null && Items.Count > 0)
            {
                CurrentItem = Items[0];
            }
        }

        public void Uninitialize()
        {
            CurrentIndex = 0;
            CurrentItem = null;
            Items = null;
        }

        public void AttemptPreviousNavigation()
        {
            if (NavigationThrottleTimer.IsEnabled)
            {
                return;
            }

            NavigationThrottleTimer.Start();

            var itemCount = Items?.Count ?? 1;
            CurrentIndex = MathHelper.Modulo(CurrentIndex - 1, itemCount);
            CurrentItem = Items?.ElementAtOrDefault(CurrentIndex);
        }

        public void AttemptNextNavigation()
        {
            if (NavigationThrottleTimer.IsEnabled)
            {
                return;
            }

            NavigationThrottleTimer.Start();

            var itemCount = Items?.Count ?? 1;
            CurrentIndex = MathHelper.Modulo(CurrentIndex + 1, itemCount);
            CurrentItem = Items?.ElementAtOrDefault(CurrentIndex);
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
