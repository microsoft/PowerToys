// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Peek.UI.Extensions;
using Peek.UI.Helpers;
using Windows.Win32.Foundation;

namespace Peek.UI.Models
{
    public class SelectedItemByWindowHandle : SelectedItem
    {
        public HWND WindowHandle { get; }

        public SelectedItemByWindowHandle(HWND windowHandle)
        {
            WindowHandle = windowHandle;
        }

        public override bool Matches(string? path)
        {
            var selectedItems = FileExplorerHelper.GetSelectedItems(WindowHandle);
            var selectedItemsCount = selectedItems?.GetCount() ?? 0;
            if (selectedItems == null || selectedItemsCount == 0 || selectedItemsCount > 1)
            {
                return false;
            }

            var fileExplorerSelectedItemPath = selectedItems.GetItemAt(0).ToIFileSystemItem().Path;
            var currentItemPath = path;
            return fileExplorerSelectedItemPath != null && currentItemPath != null && fileExplorerSelectedItemPath != currentItemPath;
        }
    }
}
