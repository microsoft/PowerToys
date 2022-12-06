// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI
{
    using System.Diagnostics;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Peek.Common.Models;
    using Peek.UI.Helpers;

    public partial class FileManager : ObservableObject
    {
        public void Uninitialize()
        {
            currentFile = null;

            // folderItems = null;
            // currentItemIndex = -1;
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

            var firstSelectedItem = selectedItems.Item(0);
            Debug.WriteLine("!~ Setting cur item to " + firstSelectedItem.Name);
            CurrentFile = new File(firstSelectedItem.Path);

            var items = selectedItems.Count > 1 ? selectedItems : folderView.Folder?.Items();
            if (items == null)
            {
                return;
            }

            // TODO:
            // folderItems = items;
        }

        [ObservableProperty]
        private File? currentFile;

        // private Shell32.FolderItems? folderItems;

        // private int currentItemIndex = -1;
    }
}
