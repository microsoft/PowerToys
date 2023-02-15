// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Peek.Common.Models;
using Peek.UI.Extensions;
using Peek.UI.Helpers;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Peek.UI
{
    public partial class FolderItemsQuery : ObservableObject
    {
        private const int UninitializedItemIndex = -1;
        private readonly object _mutateQueryDataLock = new();
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        [ObservableProperty]
        private IFileSystemItem? currentItem;

        [ObservableProperty]
        private bool isMultiSelection;

        [ObservableProperty]
        private int currentItemIndex = UninitializedItemIndex;

        [ObservableProperty]
        private int fileCount;

        private IShellItemArray? ShellItemArray { get; set; }

        private CancellationTokenSource CancellationTokenSource { get; set; } = new CancellationTokenSource();

        private Task? InitializeFilesTask { get; set; } = null;

        public void Clear()
        {
            CurrentItem = null;
            IsMultiSelection = false;

            if (InitializeFilesTask != null && InitializeFilesTask.Status == TaskStatus.Running)
            {
                Debug.WriteLine("Detected existing initializeFilesTask running. Cancelling it..");
                CancellationTokenSource.Cancel();
            }

            InitializeFilesTask = null;

            lock (_mutateQueryDataLock)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    ShellItemArray = null;
                    CurrentItemIndex = UninitializedItemIndex;
                });
            }
        }

        public void UpdateCurrentItemIndex(int desiredIndex)
        {
            if (FileCount <= 1 || CurrentItemIndex == UninitializedItemIndex ||
                (InitializeFilesTask != null && InitializeFilesTask.Status == TaskStatus.Running))
            {
                return;
            }

            // Current index wraps around when reaching min/max folder item indices
            desiredIndex %= FileCount;
            CurrentItemIndex = desiredIndex < 0 ? FileCount + desiredIndex : desiredIndex;

            if (CurrentItemIndex < 0 || CurrentItemIndex >= FileCount)
            {
                Debug.Assert(false, "Out of bounds folder item index detected.");
                CurrentItemIndex = 0;
            }

            IShellItem? shellItem = ShellItemArray?.GetItemAt(CurrentItemIndex);
            IFileSystemItem? item = shellItem?.ToIFileSystemItem();

            CurrentItem = item;
        }

        public void Start()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var foregroundWindowHandle = Windows.Win32.PInvoke.GetForegroundWindow();

            var selectedItems = FileExplorerHelper.GetSelectedItems(foregroundWindowHandle);
            var selectedItemsCount = selectedItems?.GetCount() ?? 0;
            if (selectedItems == null || selectedItemsCount < 1)
            {
                return;
            }

            bool hasMoreThanOneItem = selectedItemsCount > 1;
            IsMultiSelection = hasMoreThanOneItem;

            // Prioritize setting CurrentItem, which notifies UI
            var firstSelectedItem = selectedItems.GetItemAt(0).ToIFileSystemItem();
            CurrentItem = firstSelectedItem;

            // TODO: we shouldn't get all files from the Shell API, we should query them
            var items = hasMoreThanOneItem ? selectedItems : FileExplorerHelper.GetItems(foregroundWindowHandle);
            if (items == null)
            {
                return;
            }

            try
            {
                if (InitializeFilesTask != null && InitializeFilesTask.Status == TaskStatus.Running)
                {
                    Debug.WriteLine("Detected unexpected existing initializeFilesTask running. Cancelling it..");
                    CancellationTokenSource.Cancel();
                }

                CancellationTokenSource = new CancellationTokenSource();
                InitializeFilesTask = new Task(() => InitializeFiles(items, firstSelectedItem, CancellationTokenSource.Token));

                // Execute file initialization/querying on background thread
                InitializeFilesTask.Start();

                stopwatch.Stop();
                var elapsed = stopwatch.ElapsedMilliseconds;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception trying to run initializeFilesTask:\n" + e.ToString());
            }
        }

        // Finds index of firstSelectedItem either amongst folder items, initializing our internal File list
        //  since storing Shell32.FolderItems as a field isn't reliable.
        // Can take a few seconds for folders with 1000s of items; ensure it runs on a background thread.
        //
        // TODO optimization:
        //  Handle case where selected items count > 1 separately. Although it'll still be slow for 1000s of items selected,
        //  we can leverage faster APIs like Windows.Storage when 1 item is selected, and navigation is scoped to
        //  the entire folder. We can then avoid iterating through all items here, and maintain a dynamic window of
        //  loaded items around the current item index.
        private void InitializeFiles(
            IShellItemArray items,
            IFileSystemItem firstSelectedItem,
            CancellationToken cancellationToken)
        {
            var currentItemIndex = 0;

            cancellationToken.ThrowIfCancellationRequested();

            lock (_mutateQueryDataLock)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _dispatcherQueue.TryEnqueue(() =>
                {
                    FileCount = items.GetCount();
                    ShellItemArray = items;
                    CurrentItemIndex = currentItemIndex;
                });
            }
        }
    }
}
