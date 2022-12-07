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

            // TODO: cancel ongoing fileinit task
        }

        public void UpdateCurrentItemIndex(int desiredIndex)
        {
            // TODO: add processing check
            if (files == null || files.Count <= 1 || currentItemIndex == UninitializedItemIndex)
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
                initializeFilesTask = new Task(() => InitializeFiles(items, firstSelectedItem), cancellationTokenSource.Token);
                initializeFilesTask.Start();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception trying to run initializeFilesTask:\n" + e.ToString());
            }
        }

        // Can take a few seconds for folders with 1000s of files.
        // TODO: figure out what happens if user deletes/adds files in a very large folder while this loop runs
        // TODO [link optimization task]:
        //      - note about just storing SHell32.FolderItems not being reliable as a field for long(running into issues where it'll populate the rest of
        //          items with other files only for the first item of a folder)
        //      - note about not being able to leverage much folder api, due to having to accommodate multi-file selections
        //          -> might be worth handling those differently
        //      - can leverage FE sorted order to binary search for currnet index,
        private void InitializeFiles(Shell32.FolderItems items, Shell32.FolderItem firstSelectedItem)
        {
            var tempFiles = new List<File>(items.Count);
            var tempCurIndex = UninitializedItemIndex;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items.Item(i);
                if (item == null)
                {
                    continue;
                }

                if (item.Name.ToLower() == firstSelectedItem.Name.ToLower())
                {
                    tempCurIndex = i;
                }

                tempFiles.Add(new File(item.Path));
            }

            if (tempCurIndex == UninitializedItemIndex)
            {
                Debug.WriteLine("Folder data initialization: selectedItem index not found. Disabling navigation.");
                return;
            }

            files = tempFiles;
            currentItemIndex = tempCurIndex;

            // TODO: enable nav explicitly?
            Debug.WriteLine("!~ navigation " + firstSelectedItem.Name);
        }

        [ObservableProperty]
        private File? currentFile;

        private List<File> files = new ();

        private int currentItemIndex = UninitializedItemIndex;

        public int CurrentItemIndex => currentItemIndex;

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private Task? initializeFilesTask = null;
    }
}
