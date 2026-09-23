// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Helpers;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// One requester's interest in initializing an item. Its lifetime is independent
/// of the coordinator, and releasing it cannot cancel another requester's demand.
/// </summary>
internal sealed class ListItemInitializationDemand(ListItemViewModel item)
{
    private InterlockedBoolean _released;

    internal ListItemViewModel Item { get; } = item;

    internal bool IsActive => !_released.Value;

    internal void Release() => _released.Set();
}
