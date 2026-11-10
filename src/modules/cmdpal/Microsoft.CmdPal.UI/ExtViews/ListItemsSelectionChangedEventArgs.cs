// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI;

public sealed class ListItemsSelectionChangedEventArgs(ICommandBarContext? context) : EventArgs
{
    public ICommandBarContext? Context { get; } = context;
}
