// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ItemsChangedEventArgs : IItemsChangedEventArgs
{
    public int TotalItems { get; protected set; }

    public ItemsChangedEventArgs(int totalItems = -1)
    {
        TotalItems = totalItems;
    }
}
