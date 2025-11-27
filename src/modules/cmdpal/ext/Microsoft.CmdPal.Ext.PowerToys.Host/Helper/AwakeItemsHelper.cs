// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerToys.Helper;

// Placeholder helper for Awake commands; currently returns no items.
internal static class AwakeItemsHelper
{
    public static IListItem[] FilteredItems(string? query)
    {
        // Future Awake quick actions can be returned here when implemented.
        return Array.Empty<IListItem>();
    }
}
