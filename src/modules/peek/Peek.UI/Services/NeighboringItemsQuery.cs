// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Peek.Common.Models;
using Peek.UI.Extensions;
using Peek.UI.Helpers;
using Peek.UI.Models;

namespace Peek.UI
{
    public partial class NeighboringItemsQuery : ObservableObject
    {
        [ObservableProperty]
        private bool isMultipleFilesActivation;

        public NeighboringItems? GetNeighboringItems()
        {
            var foregroundWindowHandle = Windows.Win32.PInvoke.GetForegroundWindow();

            var selectedItemsShellArray = FileExplorerHelper.GetSelectedItems(foregroundWindowHandle);
            var selectedItemsCount = selectedItemsShellArray?.GetCount() ?? 0;

            if (selectedItemsShellArray == null || selectedItemsCount < 1)
            {
                return null;
            }

            bool hasMoreThanOneItem = selectedItemsCount > 1;
            IsMultipleFilesActivation = hasMoreThanOneItem;

            var neighboringItemsShellArray = hasMoreThanOneItem ? selectedItemsShellArray : FileExplorerHelper.GetItems(foregroundWindowHandle);
            if (neighboringItemsShellArray == null)
            {
                return null;
            }

            return new NeighboringItems(neighboringItemsShellArray);
        }
    }
}
