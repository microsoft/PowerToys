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
    using Windows.Media.Devices;

    public partial class FileManager : ObservableObject
    {
        private const int UninitializedItemIndex = -1;

        public void Uninitialize()
        {
            currentFile = null;

            files = new List<File>();
            currentItemIndex = UninitializedItemIndex;
        }

        public void UpdateCurrentItemIndex(int desiredIndex)
        {
            // TODO: add processing check
            if (files == null || currentItemIndex == UninitializedItemIndex)
            {
                Debug.WriteLine("!~ navigtion disabled");
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

            Debug.WriteLine("!~ updating cur item index " + currentItemIndex);
            CurrentFile = files[currentItemIndex];
            Debug.WriteLine("!~ Finished updating cur item idx " + currentItemIndex);
        }

        public void Initialize()
        {
            // TODO: check if task is running for whatever reason????
            Debug.WriteLine("!~ Initializing file data");
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
                // Check if cancellationTokenSource is used? else create a new one
                initializeFolderDataTask = new Task(() => InitializeFolderData(items, firstSelectedItem), cancellationTokenSource.Token);
                initializeFolderDataTask.Start();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception trying to run initializeFolderDataTask:\n" + e.ToString());
            }
        }

        // Can take a few seconds for folders with 1000s of files
        // TODO: figure out what happens if user deletes/adds files in a very large folder while this loop runs
        private void InitializeFolderData(Shell32.FolderItems items, Shell32.FolderItem firstSelectedItem)
        {
            var tempFiles = new List<File>(items.Count);

            for (int i = 0; i < items.Count; i++)
            {
                var item = items.Item(i);
                if (item == null)
                {
                    continue;
                }

                if (item.Name.ToLower() == firstSelectedItem.Name.ToLower())
                {
                    currentItemIndex = i;
                }

                tempFiles.Add(new File(item.Path));
            }

            if (currentItemIndex == UninitializedItemIndex)
            {
                Debug.WriteLine("Folder data initialization: selectedItem index not found. Disabling navigation.");
                return;
            }

            files = tempFiles;

            // TODO: enable nav?
            Debug.WriteLine("!~ navigation " + firstSelectedItem.Name);
        }

        [ObservableProperty]
        private File? currentFile;

        private List<File> files = new ();

        private int currentItemIndex = UninitializedItemIndex;

        public int CurrentItemIndex => currentItemIndex;

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private Task? initializeFolderDataTask = null;
    }
}
