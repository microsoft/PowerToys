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
    using Peek.UI.FolderItemSources;
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

        private CancellationTokenSource CancellationTokenSource { get; set; } = new CancellationTokenSource();

        private Task? InitializeQueryTask { get; set; } = null;

        private IFolderItemsSource? FolderItemsSource { get; set; } = null;

        // Must be called from UI thread
        public void Clear()
        {
            CurrentFile = null;
            IsMultiSelection = false;

            if (InitializeQueryTask != null && InitializeQueryTask.Status == TaskStatus.Running)
            {
                Debug.WriteLine("Detected existing InitializeQueryTask running. Cancelling it..");
                CancellationTokenSource.Cancel();
            }

            InitializeQueryTask = null;

            lock (_mutateQueryDataLock)
            {
                ItemsCount = 0; // TODO: can maybe move outside?
                CurrentItemIndex = UninitializedItemIndex;
            }
        }

        // Must be called from UI thread
        public async Task UpdateCurrentItemIndex(int desiredIndex)
        {
            // TODO: add items count check
            if (ItemsCount <= 1 || CurrentItemIndex == UninitializedItemIndex || FolderItemsSource == null ||
                (InitializeQueryTask != null && InitializeQueryTask.Status == TaskStatus.Running))
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

            if (FolderItemsSource == null)
            {
                return;
            }

            // TODO: safety checks + bounds checks + refactor int field to uint for extra safety
            // TODO: add note about time taken
            CurrentFile = await FolderItemsSource.GetItemAt((uint)CurrentItemIndex); // TODO: fix uint declarations
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
                if (InitializeQueryTask != null && InitializeQueryTask.Status == TaskStatus.Running)
                {
                    Debug.WriteLine("Detected unexpected existing InitializeQueryTask running. Cancelling it..");
                    CancellationTokenSource.Cancel();
                }

                CancellationTokenSource = new CancellationTokenSource();
                InitializeQueryTask = new Task(() => InitializeQuery(folderView, items, firstSelectedItem, CancellationTokenSource.Token));

                // Execute file initialization/querying on background thread
                InitializeQueryTask.Start();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception trying to run InitializeQueryTask:\n" + e.ToString());
            }
        }

        private async void InitializeQuery(
            Shell32.IShellFolderViewDual3 folderView, // TODO: remove
            Shell32.FolderItems items,
            Shell32.FolderItem firstSelectedItem,
            CancellationToken cancellationToken)
        {
            FolderItemsSource = IsMultiSelection ? new SelectedItemsSource() : new WholeFolderItemsSource();

            InitialQueryData? initialQueryData = await FolderItemsSource.Initialize(folderView);
            if (initialQueryData == null || !initialQueryData.HasValue)
            {
                return;
            }

            InitialQueryData y = new ();
            y.FirstItemIndex = 0;

            cancellationToken.ThrowIfCancellationRequested();

            // Lock to prevent race conditions with UI thread's Clear calls upon Peek deactivation/hide
            lock (_mutateQueryDataLock)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _dispatcherQueue.TryEnqueue(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ItemsCount = (int)initialQueryData.Value.ItemsCount; // TODO: remove cast
                    CurrentItemIndex = (int)initialQueryData.Value.FirstItemIndex;
                });
            }
        }
    }
}
