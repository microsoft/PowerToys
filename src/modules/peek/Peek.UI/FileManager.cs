// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI
{
    using System.Diagnostics;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Peek.Common.Models;
    using Peek.UI.Helpers;
    using Windows.Media.Devices;

    public partial class FileManager : ObservableObject
    {
        public void Uninitialize()
        {
            currentFile = null;

            folderItems = null;
            currentItemIndex = -1;
        }

        public void UpdateCurrentItemIndex(int desiredIndex)
        {
            // TODO: add processing check
            if (folderItems == null)
            {
                return;
            }

            currentItemIndex = desiredIndex % folderItems.Count;
            CurrentFile = new File(folderItems.Item(currentItemIndex).Path);
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
            // TODO: double check how lb resolves cur activated file (is just a manual search)
            folderItems = selectedItems.Count > 1 ? selectedItems : folderView.Folder?.Items();
            if (folderItems == null)
            {
                return;
            }

            // TODO: refactor to bg task
            for (int i = 0; i < folderItems.Count; i++)
            {
                if (folderItems.Item(i).Name.ToLower() == firstSelectedItem.Name.ToLower())
                {
                    currentItemIndex = i;
                    break;
                }
            }

            Debug.WriteLine("!~ Setting cur item to " + firstSelectedItem.Name);
        }

        [ObservableProperty]
        private File? currentFile;

        private Shell32.FolderItems? folderItems;

        private int currentItemIndex = -1;

        public int CurrentItemIndex { get => currentItemIndex; }
    }
}
