// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
        /// The direction of the most recent navigation request, buffered while the
        /// pacing timer is running. <see langword="null"/> when no navigation is pending.
        /// </summary>
        private NavigationDirection? _pendingNavigationDirection;

        /// <summary>
        /// The delay in milliseconds before a delete operation begins, to allow for navigation
        /// away from the current item to occur.
        /// </summary>
        private const int DeleteDelayMs = 200;

        /// <summary>
        /// Holds the indexes of each <see cref="IFileSystemItem"/> the user has deleted.
        /// </summary>
        private readonly HashSet<int> _deletedItemIndexes = [];

        private static readonly string _defaultWindowTitle = ResourceLoaderInstance.GetString("AppTitle/Title");

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

        /// <summary>
        /// Work around missing navigation when peeking from CLI.
        /// TODO: Implement navigation when peeking from CLI.
        /// </summary>
        private bool _isFromCli;

        partial void OnCurrentItemChanged(IFileSystemItem? value)
        {
            WindowTitle = value != null
                ? ResourceLoaderInstance.FormatString("WindowTitle", value.Name)
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

        private readonly DispatcherTimer _navigationPacerTimer = new();

        public MainWindowViewModel(NeighboringItemsQuery query)
        {
            NeighboringItemsQuery = query;
            WindowTitle = _defaultWindowTitle;

            _navigationPacerTimer.Interval = TimeSpan.FromMilliseconds(NavigationThrottleDelayMs);
            _navigationPacerTimer.Tick += NavigationPacerTimer_Tick;
        }

        public void Initialize(SelectedItem selectedItem)
        {
            switch (selectedItem)
            {
                case SelectedItemByPath selectedItemByPath:
                    InitializeFromCli(selectedItemByPath.Path);
                    break;

                case SelectedItemByWindowHandle selectedItemByWindowHandle:
                    InitializeFromExplorer(selectedItemByWindowHandle.WindowHandle);
                    break;

                default:
                    throw new NotImplementedException($"Invalid type of selected item: '{selectedItem.GetType().FullName}'");
            }
        }

        private void InitializeFromExplorer(HWND foregroundWindowHandle)
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
            _isFromCli = false;

            CurrentItem = (Items != null && Items.Count > 0) ? Items[0] : null;
        }

        private void InitializeFromCli(string path)
        {
            // TODO: implement navigation
            _isFromCli = true;
            Items = null;
            _currentIndex = DisplayIndex = 0;
            CurrentItem = new FileItem(path, Path.GetFileName(path));
        }

        public void Uninitialize()
        {
            _navigationPacerTimer.Stop();
            _pendingNavigationDirection = null;
            _currentIndex = DisplayIndex = 0;
            CurrentItem = null;
            _deletedItemIndexes.Clear();
            Items = null;
            _navigationDirection = NavigationDirection.Forwards;
            IsErrorVisible = false;
            _isFromCli = false;
        }

        public void AttemptPreviousNavigation() => RequestNavigate(NavigationDirection.Backwards);

        public void AttemptNextNavigation() => RequestNavigate(NavigationDirection.Forwards);

        private void RequestNavigate(NavigationDirection direction)
        {
            if (_isFromCli || Items is null || Items.Count == _deletedItemIndexes.Count)
            {
                return;
            }

            if (!_navigationPacerTimer.IsEnabled)
            {
                // First press or idle: Navigate immediately and start the cooldown timer.
                Navigate(direction);
                _navigationPacerTimer.Start();
            }
            else
            {
                // Coalesce rapid repeats/taps to a single pending navigation step.
                _pendingNavigationDirection = direction;
            }
        }

        private void NavigationPacerTimer_Tick(object? sender, object e)
        {
            if (_pendingNavigationDirection.HasValue)
            {
                var direction = _pendingNavigationDirection.Value;
                _pendingNavigationDirection = null;

                Navigate(direction);
            }
            else
            {
                _navigationPacerTimer.Stop();
            }
        }

        private void Navigate(NavigationDirection direction, bool isAfterDelete = false)
        {
            // Check if every item in the folder/selection has been deleted.
            if (Items is null || Items.Count == _deletedItemIndexes.Count)
            {
                _currentIndex = DisplayIndex = 0;
                CurrentItem = null;
                return;
            }

            _navigationDirection = direction;

            int offset = direction == NavigationDirection.Forwards ? 1 : -1;

            do
            {
                // Items cannot be null here.
                _currentIndex = MathHelper.Modulo(_currentIndex + offset, Items!.Count);
            }
            while (_deletedItemIndexes.Contains(_currentIndex));

            // If we're navigating forwards after a delete operation, the displayed index does not
            // change, e.g. "(2/3)" becomes "(2/2)".
            if (isAfterDelete && direction == NavigationDirection.Forwards)
            {
                offset = 0;
            }

            DisplayIndex = MathHelper.Modulo(DisplayIndex + offset, DisplayItemCount);
            CurrentItem = Items[_currentIndex];
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

            // Cancel any queued key-repeat navigation, as delete takes immediate priority.
            _pendingNavigationDirection = null;
            _navigationPacerTimer.Stop();

            // Mark the item as deleted and update the displayed item count.
            int index = _currentIndex;
            _deletedItemIndexes.Add(index);
            OnPropertyChanged(nameof(DisplayItemCount));

            // Attempt the deletion then navigate to the next file. Captures item and index
            // by value; CurrentItem may change before the delay completes.
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

        /// <summary>
        /// Removes the item from the deleted set if the file still exists on disk, e.g.
        /// after a cancelled or failed delete.
        /// </summary>
        /// <param name="item">The item to reinstate.</param>
        /// <param name="index">The index of the item in the deleted set.</param>
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

        public void ShowError(string message)
        {
            IsErrorVisible = false;
            ErrorMessage = message;
            IsErrorVisible = true;
        }
    }
}
