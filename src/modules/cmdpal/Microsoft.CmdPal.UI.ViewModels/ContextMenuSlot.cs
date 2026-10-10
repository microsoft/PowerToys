// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// A factory-declared position for a host command resolved by the owning item.
/// </summary>
public sealed class ContextMenuSlot : IContextItemViewModel
{
    public static ContextMenuSlot ShowDetails { get; } = new();

    private ContextMenuSlot()
    {
    }
}
