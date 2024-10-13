// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.UI.Models;
using Windows.Win32.Foundation;
using static Peek.UI.Native.NativeMethods;

namespace Peek.UI
{
    public partial class MainWindowViewModel : ObservableObject
    {
        /// <summary>
        /// The minimum time in milliseconds between navigation events.
        /// </summary>
        private const int NavigationThrottleDelayMs = 100;

        /// <summary>
        /// The delay in milliseconds before a delete operation begins, to allow for navigation
        /// away from the current item to occur.
        /// </summary>
        private const int DeleteDelayMs = 200;

        /// <summary>
        /// Holds the indexes of each <see cref="IFileSystemItem"/> the user has deleted.
        /// </summary>
        private readonly HashSet<int> _deletedItemIndexes = [];

        private static readonly string _defaultWindowTitle = ResourceLoaderInstance.ResourceLoader.GetString("AppTitle/Title");

        /// <summary>
        /// The actual index of the current item in the items array. Does not necessarily
        /// correspond to <see cref="_displayIndex"/> if one or more files have been deleted.
        /// </summary>
        private int _currentIndex;

        /// <summary>
        /// The item index to display in the titlebar.
        /// </summary>
        [ObservableProperty]
        private int _displayIndex;

        /// <summary>
        /// The item to be displayed by a matching previewer. May be null if the user has deleted
        /// all items.
        /// </summary>
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
        [NotifyPropertyChangedFor(nameof(DisplayItemCount))]
        private NeighboringItems? _items;

        /// <summary>
        /// The number of items selected and available to preview. Decreases as the user deletes
        /// items. Displayed on the title bar.
        /// </summary>
        private int _displayItemCount;

        public int DisplayItemCount
        {
            get => Items?.Count - _deletedItemIndexes.Count ?? 0;
            set
            {
                if (_displayItemCount != value)
                {
                    _displayItemCount = value;
                    OnPropertyChanged();
                }
            }
        }

        [ObservableProperty]
        private double _scalingFactor = 1.0;

        private enum NavigationDirection
        {
            Forwards,
            Backwards,
        }

        /// <summary>
        /// The current direction in which the user is moving through the items collection.
        /// Determines how we act when a file is deleted.
        /// </summary>
        private NavigationDirection _navigationDirection = NavigationDirection.Forwards;

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
                Logger.LogError("Failed to get File Explorer Items.", ex);
            }

            _currentIndex = DisplayIndex = 0;

            CurrentItem = (Items != null && Items.Count > 0) ? Items[0] : null;
        }

        public void Uninitialize()
        {
            _currentIndex = DisplayIndex = 0;
            CurrentItem = null;
            _deletedItemIndexes.Clear();
            Items = null;
            _navigationDirection = NavigationDirection.Forwards;
        }

        public void AttemptPreviousNavigation() => Navigate(NavigationDirection.Backwards);

        public void AttemptNextNavigation() => Navigate(NavigationDirection.Forwards);

        private void Navigate(NavigationDirection direction, bool isAfterDelete = false)
        {
            if (NavigationThrottleTimer.IsEnabled)
            {
                return;
            }

            if (Items == null || Items.Count == _deletedItemIndexes.Count)
            {
                _currentIndex = DisplayIndex = 0;
                CurrentItem = null;
                return;
            }

            _navigationDirection = direction;

            int offset = direction == NavigationDirection.Forwards ? 1 : -1;

            do
            {
                _currentIndex = MathHelper.Modulo(_currentIndex + offset, Items.Count);
            }
            while (_deletedItemIndexes.Contains(_currentIndex));

            CurrentItem = Items[_currentIndex];

            // If we're navigating forwards after a delete operation, the displayed index does not
            // change, e.g. "(2/3)" becomes "(2/2)".
            if (isAfterDelete && direction == NavigationDirection.Forwards)
            {
                offset = 0;
            }

            DisplayIndex = MathHelper.Modulo(DisplayIndex + offset, DisplayItemCount);

            NavigationThrottleTimer.Start();
        }

        /// <summary>
        /// Sends the current item to the Recycle Bin.
        /// </summary>
        public void DeleteItem()
        {
            if (CurrentItem == null || !IsFilePath(CurrentItem.Path))
            {
                return;
            }

            _deletedItemIndexes.Add(_currentIndex);
            OnPropertyChanged(nameof(DisplayItemCount));

            string path = CurrentItem.Path;

            DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            {
                Task.Delay(DeleteDelayMs);
                DeleteFile(path);
            });

            Navigate(_navigationDirection, isAfterDelete: true);
        }

        private void DeleteFile(string path, bool permanent = false)
        {
            SHFILEOPSTRUCT fileOp = new()
            {
                wFunc = FO_DELETE,
                pFrom = path + "\0\0",
                fFlags = (ushort)(FOF_NOCONFIRMATION | (permanent ? 0 : FOF_ALLOWUNDO)),
            };

            int result = SHFileOperation(ref fileOp);

            if (result != 0)
            {
                string warning = "Could not delete file. " +
                    (DeleteFileErrors.TryGetValue(result, out string? errorMessage) ? errorMessage : $"Error code {result}.");
                Logger.LogWarning(warning);
            }
        }

        private static bool IsFilePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                return (attributes & FileAttributes.Directory) != FileAttributes.Directory;
            }
            catch (Exception)
            {
                return false;
            }
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
