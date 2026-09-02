// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;

namespace Microsoft.CmdPal.UI;

public interface IListInteractionSource : IPageInteractionEventSource
{
    event EventHandler<ListItemsSelectionChangedEventArgs>? SelectionChanged;

    event EventHandler<ListItemsContextMenuRequestedEventArgs>? ContextMenuRequested;
}
