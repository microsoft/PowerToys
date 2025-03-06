// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.UI.Helpers;
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

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isErrorVisible = false;

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
            IsErrorVisible = false;
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
        /// <param name="skipConfirmationChecked">The IsChecked property of the "Don't ask me
        /// again" checkbox on the delete confirmation dialog.</param>
        public void DeleteItem(bool? skipConfirmationChecked, nint hwnd)
        {
            if (CurrentItem == null)
            {
                return;
            }

            bool skipConfirmation = skipConfirmationChecked ?? false;
            bool shouldShowConfirmation = !skipConfirmation;
            Application.Current.GetService<IUserSettings>().ConfirmFileDelete = shouldShowConfirmation;

            var item = CurrentItem;

            if (File.Exists(item.Path) && !IsFilePath(item.Path))
            {
                // The path is to a folder, not a file, or its attributes could not be retrieved.
                return;
            }

            // Update the file count and total files.
            int index = _currentIndex;
            _deletedItemIndexes.Add(index);
            OnPropertyChanged(nameof(DisplayItemCount));

            // Attempt the deletion then navigate to the next file.
            DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
            {
                await Task.Delay(DeleteDelayMs);
                int result = DeleteFile(item, hwnd);

                if (result == 0)
                {
                    // Success.
                    return;
                }

                if (result == ERROR_CANCELLED)
                {
                    if (Path.GetPathRoot(item.Path) is string root)
                    {
                        var driveInfo = new DriveInfo(root);
                        Logger.LogInfo($"User cancelled item deletion on {driveInfo.DriveType} drive.");
                    }
                }
                else
                {
                    // For failures other than user cancellation, log the error and show a message
                    // in the UI.
                    DeleteErrorMessageHelper.LogError(result);
                    ShowDeleteError(item.Name, result);
                }

                // For all errors, reinstate the deleted file if it still exists.
                ReinstateDeletedFile(item, index);
            });

            Navigate(_navigationDirection, isAfterDelete: true);
        }

        /// <summary>
        /// Delete a file by moving it to the Recycle Bin. Refresh any shell listeners.
        /// </summary>
        /// <param name="item">The item to delete.</param>
        /// <param name="hwnd">The handle of the main window.</param>
        /// <returns>The result of the file operation call. A non-zero result indicates failure.
        /// </returns>
        private int DeleteFile(IFileSystemItem item, nint hwnd)
        {
            // Move to the Recycle Bin and warn about permanent deletes.
            var flags = (ushort)(FOF_ALLOWUNDO | FOF_WANTNUKEWARNING);

            SHFILEOPSTRUCT fileOp = new()
            {
                wFunc = FO_DELETE,
                pFrom = item.Path + "\0\0", // Path arguments must be double null-terminated.
                fFlags = flags,
                hwnd = hwnd,
            };

            int result = SHFileOperation(ref fileOp);
            if (result == 0)
            {
                SendDeleteChangeNotification(item.Path);
            }

            return result;
        }

        private void ReinstateDeletedFile(IFileSystemItem item, int index)
        {
            if (File.Exists(item.Path))
            {
                _deletedItemIndexes.Remove(index);
                OnPropertyChanged(nameof(DisplayItemCount));
            }
        }

        /// <summary>
        /// Informs shell listeners like Explorer windows that a delete operation has occurred.
        /// </summary>
        /// <param name="path">Full path to the file which was deleted.</param>
        private void SendDeleteChangeNotification(string path)
        {
            IntPtr pathPtr = Marshal.StringToHGlobalUni(path);
            try
            {
                if (pathPtr == IntPtr.Zero)
                {
                    Logger.LogError("Could not allocate memory for path string.");
                }
                else
                {
                    SHChangeNotify(SHCNE_DELETE, SHCNF_PATH, pathPtr, IntPtr.Zero);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pathPtr);
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

        private void ShowDeleteError(string filename, int errorCode)
        {
            IsErrorVisible = false;
            ErrorMessage = DeleteErrorMessageHelper.GetUserErrorMessage(filename, errorCode);
            IsErrorVisible = true;
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
