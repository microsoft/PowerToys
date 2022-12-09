// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.FolderItemSources
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Peek.Common.Models;
    using Shell32;
    using Windows.Storage;
    using Windows.Storage.Search;

    // Provides folder items across an entire folder
    public class WholeFolderItemsSource : IFolderItemsSource
    {
        private StorageItemQueryResult? ItemQuery { get; set; } = null;

        public async Task<File?> GetItemAt(uint index)
        {
            if (ItemQuery == null)
            {
                return null;
            }

            IReadOnlyList<IStorageItem> items;
            try
            {
                // ~1ms runtime on workstation w/ a debugger attached.
                // TODO: further optimize by pre-fetching and maintaining a window of items.
                //  There's a win32 API FindNextFile we could have used, but it doesn't allow fast, reverse iteration,
                //  which is needed for backwards navigation.
                items = await ItemQuery.GetItemsAsync(index, 1);
                if (items == null || items.Count == 0)
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Caught exception attempting to get file:\n", e.ToString());
                return null;
            }

            // TODO: optimize by adding StorageItem as field to File class
            return new File(items.First().Path);
        }

        public async Task<InitialQueryData?> Initialize(IShellFolderViewDual3 folderView)
        {
            try
            {
                var selectedItems = folderView.SelectedItems();
                if (selectedItems == null || selectedItems.Count == 0)
                {
                    return null;
                }

                Debug.Assert(selectedItems.Count == 1, "SelectedItemsSource is intended for multi-item activations");

                var selectedItem = selectedItems.Item(0);
                var parent = System.IO.Directory.GetParent(selectedItem.Path); // TODO: try get directory name instead
                if (parent == null)
                {
                    return null;
                }

                var folder = await StorageFolder.GetFolderFromPathAsync(parent.FullName);

                // TODO: check if query options are supported (member helpers)
                var queryOptions = new QueryOptions();
                queryOptions.IndexerOption = IndexerOption.UseIndexerWhenAvailable;

                // TODO: check if this clear is actually needed
                Debug.WriteLine("count: " + queryOptions.SortOrder.Count);
                queryOptions.SortOrder.Clear();

                // TODO: fetch sort option
                queryOptions.SortOrder.Add(new SortEntry("System.Size", false));

                ItemQuery = folder.CreateItemQuery();
                ItemQuery.ApplyNewQueryOptions(queryOptions);

                Debug.WriteLine(selectedItem.Name);

                // TODO: property passed in depends on sort order passed to query
                var firstItemIndex = await ItemQuery.FindStartIndexAsync(selectedItem.Size);

                // FindStartIndexAsync returns this when no item found
                if (firstItemIndex == uint.MaxValue)
                {
                    Debug.WriteLine("File not found");
                    return null;
                }

                InitialQueryData result = new ();

                // TODO: pass & throw cancellation token (not essential, but may free thread resources earlier)
                result.ItemsCount = await ItemQuery.GetItemCountAsync();
                result.FirstItemIndex = firstItemIndex;
                return result;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
