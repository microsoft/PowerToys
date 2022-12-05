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

    public partial class MainWindowViewModel : ObservableObject
    {
        public void ClearFileData()
        {
            Files = new List<File>();
            CurrentFile = null;

            // TODO: cancel ongoing file fetch task
        }

        public void InitializeFileData()
        {
            Debug.WriteLine("!~ Initializing file data");
            var folderView = FileExplorerHelper.GetCurrentFolderView();
            if (folderView == null)
            {
                // TODO: notify view? maybe no need
                return;
            }

            Shell32.FolderItems selectedItems = folderView.SelectedItems();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                // TODO: notify view? maybe no need
                return;
            }

            var firstSelectedItem = selectedItems.Item(0);
            Debug.WriteLine("!~ Setting cur item to " + firstSelectedItem.Name);
            CurrentFile = new File(firstSelectedItem.Path);

            // TODO: check if selected items > 1; to see if we should make this the folder item list
            // foreach (Shell32.FolderItem item in selectedItems)
            // {
            //    selectedItems.Add(new File(item.Path));
            // }

            // Shell32.FolderItems items = folderView.Folder.Items();

            // TODO: set files
        }

        [ObservableProperty]
        private File? currentFile;

        [ObservableProperty]
        private List<File> files = new ();

        // private CancellationTokenSource fileFetchCancellationSource = default(CancellationTokenSource);
    }
}
