// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public static File? GetFileExplorerSelectedFile()
        {
            var shellItems = FileExplorerHelper.GetSelectedItems();
            var firstSelectedItem = shellItems?.Item(0);
            if (shellItems == null || firstSelectedItem == null)
            {
                return null;
            }

            return new File(firstSelectedItem.Path);
        }

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
            var folderView = FileExplorerHelper.GetCurrentFolderView();
            if (folderView == null)
            {
                return;
            }

            Shell32.FolderItems selectedItems = folderView.SelectedItems();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                return;
            }

            IsMultiSelection = selectedItems.Count > 1;

            // Prioritize setting CurrentFile, which notifies UI
            var firstSelectedItem = selectedItems.Item(0);
            CurrentFile = new File(firstSelectedItem.Path);

            var items = selectedItems.Count > 1 ? selectedItems : folderView.Folder?.Items();
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
            Shell32.FolderItems items,
            Shell32.FolderItem firstSelectedItem,
            CancellationToken cancellationToken)
        {
            var tempFiles = new List<File>(items.Count);
            var tempCurIndex = UninitializedItemIndex;

            for (int i = 0; i < items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var item = items.Item(i);
                if (item == null)
                {
                    continue;
                }

                if (item.Name == firstSelectedItem.Name)
                {
                    tempCurIndex = i;
                }

                tempFiles.Add(new File(item.Path));
            }

            if (tempCurIndex == UninitializedItemIndex)
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
                    Files = tempFiles;
                    CurrentItemIndex = tempCurIndex;
                });
            }
        }
    }
}
