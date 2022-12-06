// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI
{
    using System.Collections.Generic;
    using System.Diagnostics;
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

            // Since parent folder can contain 1000s of items, process them on bg thread
            // TODO: kick off background task processing items (to find idx)
            var items = selectedItems.Count > 1 ? selectedItems : folderView.Folder?.Items();
            if (items == null)
            {
                return;
            }

            // TODO: refactor to bg task, give better name
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

            files = tempFiles;

            Debug.WriteLine("!~ Setting cur item to " + firstSelectedItem.Name);
        }

        [ObservableProperty]
        private File? currentFile;

        private List<File> files = new ();

        private int currentItemIndex = UninitializedItemIndex;

        public int CurrentItemIndex => currentItemIndex;
    }
}
