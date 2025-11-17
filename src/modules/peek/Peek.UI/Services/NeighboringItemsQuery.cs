// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Peek.UI.Helpers;
using Peek.UI.Models;

namespace Peek.UI
{
    public partial class NeighboringItemsQuery : ObservableObject
    {
        [ObservableProperty]
        private bool isMultipleFilesActivation;

        public NeighboringItems? GetNeighboringItems(Windows.Win32.Foundation.HWND foregroundWindowHandle)
        {
            var selectedItemsShellArray = FileExplorerHelper.GetSelectedItems(foregroundWindowHandle);
            var selectedItemsCount = selectedItemsShellArray?.GetCount() ?? 0;

            if (selectedItemsShellArray == null || selectedItemsCount < 1)
            {
                return null;
            }

            bool hasMoreThanOneItem = selectedItemsCount > 1;
            IsMultipleFilesActivation = hasMoreThanOneItem;

            var neighboringItemsShellArray = hasMoreThanOneItem ? selectedItemsShellArray : FileExplorerHelper.GetItems(foregroundWindowHandle);
            return neighboringItemsShellArray == null ? null : new NeighboringItems(neighboringItemsShellArray);
        }
    }
}
