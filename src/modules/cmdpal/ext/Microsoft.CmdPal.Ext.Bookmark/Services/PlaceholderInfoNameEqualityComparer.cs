// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Bookmarks.Services;

public class PlaceholderInfoNameEqualityComparer : IEqualityComparer<PlaceholderInfo>
{
    public static PlaceholderInfoNameEqualityComparer Instance { get; } = new();

    public bool Equals(PlaceholderInfo? x, PlaceholderInfo? y)
    {
        if (x is null && y is null)
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(PlaceholderInfo obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name);
    }
}
