// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Peek.Common.Models;
using Peek.UI.Helpers;

namespace Peek.UI
{
    public partial class FolderItemsQuery : ObservableObject
    {
        private const int UninitializedItemIndex = -1;
        private readonly object _mutateQueryDataLock = new();
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        [ObservableProperty]
        private File? currentFile;

        [ObservableProperty]
        private List<File> files = new();

        [ObservableProperty]
        private bool isMultiSelection;

        [ObservableProperty]
        private int currentItemIndex = UninitializedItemIndex;

        private CancellationTokenSource CancellationTokenSource { get; set; } = new CancellationTokenSource();

        private Task? InitializeFilesTask { get; set; } = null;

        public void Clear()
        {
            CurrentFile = null;
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
                    Files = new List<File>();
                    CurrentItemIndex = UninitializedItemIndex;
                });
            }
        }

        public void UpdateCurrentItemIndex(int desiredIndex)
        {
            if (Files.Count <= 1 || CurrentItemIndex == UninitializedItemIndex ||
                (InitializeFilesTask != null && InitializeFilesTask.Status == TaskStatus.Running))
            {
                return;
            }

            // Current index wraps around when reaching min/max folder item indices
            desiredIndex %= Files.Count;
            CurrentItemIndex = desiredIndex < 0 ? Files.Count + desiredIndex : desiredIndex;

            if (CurrentItemIndex < 0 || CurrentItemIndex >= Files.Count)
            {
                Debug.Assert(false, "Out of bounds folder item index detected.");
                CurrentItemIndex = 0;
            }

            CurrentFile = Files[CurrentItemIndex];
        }

        public void Start()
        {
            var selectedItems = FileExplorerHelper.GetSelectedItems();
            if (!selectedItems.Any())
            {
                return;
            }

            bool hasMoreThanOneItem = selectedItems.Skip(1).Any();
            IsMultiSelection = hasMoreThanOneItem;

            // Prioritize setting CurrentFile, which notifies UI
            var firstSelectedItem = selectedItems.First();
            CurrentFile = firstSelectedItem;

            // TODO: we shouldn't get all files from the SHell API, we should query them
            var items = hasMoreThanOneItem ? selectedItems : FileExplorerHelper.GetItems();
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
            IEnumerable<File> items,
            File firstSelectedItem,
            CancellationToken cancellationToken)
        {
            var listOfItems = items.ToList();
            var currentItemIndex = listOfItems.FindIndex(item => item.Path == firstSelectedItem.Path);

            if (currentItemIndex < 0)
            {
                Debug.WriteLine("File query initialization: selectedItem index not found. Navigation remains disabled.");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            lock (_mutateQueryDataLock)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _dispatcherQueue.TryEnqueue(() =>
                {
                    Files = listOfItems;
                    CurrentItemIndex = currentItemIndex;
                });
            }
        }
    }
}
