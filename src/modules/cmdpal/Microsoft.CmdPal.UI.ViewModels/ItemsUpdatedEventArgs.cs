// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class ItemsUpdatedEventArgs : EventArgs
{
    public bool ForceFirstItem { get; }

    public bool EnsureSelectionVisible { get; }

    public ItemsUpdatedEventArgs(bool forceFirstItem)
        : this(forceFirstItem, ensureSelectionVisible: true)
    {
    }

    public ItemsUpdatedEventArgs(bool forceFirstItem, bool ensureSelectionVisible)
    {
        ForceFirstItem = forceFirstItem;
        EnsureSelectionVisible = ensureSelectionVisible;
    }
}
