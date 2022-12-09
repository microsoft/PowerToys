// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.UI.Dispatching;
    using Peek.Common.Models;
    using Peek.UI.Helpers;
    using Windows.Storage;
    using Windows.Storage.Search;

    public partial class FolderItemsQuery : ObservableObject
    {
        private const int UninitializedItemIndex = -1;
        private readonly object _mutateQueryDataLock = new ();
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        [ObservableProperty]
        private File? currentFile;

        [ObservableProperty]
        private int itemsCount = 0;

        [ObservableProperty]
        private bool isMultiSelection;

        [ObservableProperty]
        private int currentItemIndex = UninitializedItemIndex;

        private StorageItemQueryResult? ItemQuery { get; set; } = null;

        private CancellationTokenSource CancellationTokenSource { get; set; } = new CancellationTokenSource();

        private Task? InitializeFilesTask { get; set; } = null;

        // Must be called from UI thread
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
                ItemsCount = 0; // TODO: can maybe move outside?
                CurrentItemIndex = UninitializedItemIndex;
            }
        }

        // Must be called from UI thread
        public async void UpdateCurrentItemIndex(int desiredIndex)
        {
            // TODO: add items count check
            if (CurrentItemIndex == UninitializedItemIndex ||
                (InitializeFilesTask != null && InitializeFilesTask.Status == TaskStatus.Running))
            {
                return;
            }

            // Current index wraps around when reaching min/max folder item indices
            desiredIndex %= itemsCount;
            CurrentItemIndex = desiredIndex < 0 ? itemsCount + desiredIndex : desiredIndex;

            if (CurrentItemIndex < 0 || CurrentItemIndex >= itemsCount)
            {
                Debug.Assert(false, "Out of bounds folder item index detected.");
                CurrentItemIndex = 0;
            }

            if (ItemQuery == null)
            {
                return;
            }

            // TODO: safety checks + bounds checks + refactor int field to uint for extra safety
            // TODO: add note about time taken
            var items = await ItemQuery.GetItemsAsync((uint)currentItemIndex, 1);

            // TODO: optimize by passing in storageitem immediately
            CurrentFile = new File(items.First().Path);
        }

        // Must be called from UI thread
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
                InitializeFilesTask = new Task(() => InitializeFiles(folderView, items, firstSelectedItem, CancellationTokenSource.Token));

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
        private async void InitializeFiles(
            Shell32.IShellFolderViewDual3 folderView, // TODO: remove
            Shell32.FolderItems items,
            Shell32.FolderItem firstSelectedItem,
            CancellationToken cancellationToken)
        {
            // TODO: handle selected items separately
            var tempFiles = new List<File>(items.Count);
            var tempCurIndex = UninitializedItemIndex;

            // TODO: dealing with hidden/system items
            var parentPath = System.IO.Directory.GetParent(firstSelectedItem.Path); // TODO: rename/optimize/try+catch
            if (parentPath == null)
            {
                return;
            }

            var folder = await StorageFolder.GetFolderFromPathAsync(parentPath.FullName);

            // TODO: check if query options are supported (member helpers)
            // TODO: ensure correct query option is set <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
            var queryOptions = new QueryOptions();

            // queryOptions.IndexerOption
            try
            {
                queryOptions.SortOrder.Clear();
                queryOptions.SortOrder.Add(new SortEntry("System.Size", false));

                // queryOptions.SortOrder.Clear();
                // queryOptions.SortOrder.Add(new SortEntry("System.ItemNameDisplay", false));
                ItemQuery = folder.CreateItemQuery();
                ItemQuery.ApplyNewQueryOptions(queryOptions);

                Debug.WriteLine(firstSelectedItem.Name);

                // TODO: property passed in depends on sort order passed to query
                var idx = await ItemQuery.FindStartIndexAsync(firstSelectedItem.Size);
                if (idx == uint.MaxValue)
                {
                    Debug.WriteLine("File not found");
                    return;
                }

                tempCurIndex = (int)idx;

                Debug.WriteLine("selected index: " + tempCurIndex);
            }
            catch (Exception)
            {
            }

            cancellationToken.ThrowIfCancellationRequested();

            lock (_mutateQueryDataLock)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _dispatcherQueue.TryEnqueue(() =>
                {
                    ItemsCount = items.Count; // TODO: don't need to set this here anymore? Not reliable
                    CurrentItemIndex = tempCurIndex;
                });
            }
        }
    }
}
