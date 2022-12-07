// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Peek.Common.Models;
    using Peek.UI.Helpers;

    public partial class FolderItemsQuery : ObservableObject
    {
        private const int UninitializedItemIndex = -1;

        public void Clear()
        {
            CurrentFile = null;

            if (initializeFilesTask != null && initializeFilesTask.Status == TaskStatus.Running)
            {
                Debug.WriteLine("Detected existing initializeFilesTask running. Cancelling it..");
                cancellationTokenSource.Cancel();
            }

            initializeFilesTask = null;

            lock (mutateQueryDataLock)
            {
                files = new List<File>();
                currentItemIndex = UninitializedItemIndex;
            }
        }

        public void UpdateCurrentItemIndex(int desiredIndex)
        {
            if (files == null || files.Count <= 1 || currentItemIndex == UninitializedItemIndex ||
                (initializeFilesTask != null && initializeFilesTask.Status == TaskStatus.Running))
            {
                return;
            }

            // Current index wraps around when reaching min/max folder item indices
            desiredIndex %= files.Count;
            currentItemIndex = desiredIndex < 0 ? files.Count + desiredIndex : desiredIndex;

            if (currentItemIndex < 0 || currentItemIndex >= files.Count)
            {
                Debug.Assert(false, "Out of bounds folder item index detected.");
                currentItemIndex = 0;
            }

            CurrentFile = files[currentItemIndex];
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
                if (initializeFilesTask != null && initializeFilesTask.Status == TaskStatus.Running)
                {
                    Debug.WriteLine("Detected unexpected existing initializeFilesTask running. Cancelling it..");
                    cancellationTokenSource.Cancel();
                }

                cancellationTokenSource = new CancellationTokenSource();
                initializeFilesTask = new Task(() => InitializeFiles(items, firstSelectedItem, cancellationTokenSource.Token));

                // Execute file initialization/querying on background thread
                initializeFilesTask.Start();
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

            lock (mutateQueryDataLock)
            {
                cancellationToken.ThrowIfCancellationRequested();
                files = tempFiles;
                currentItemIndex = tempCurIndex;
            }
        }

        private readonly object mutateQueryDataLock = new ();

        [ObservableProperty]
        private File? currentFile;

        private List<File> files = new ();

        private int currentItemIndex = UninitializedItemIndex;

        public int CurrentItemIndex => currentItemIndex;

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private Task? initializeFilesTask = null;
    }
}
