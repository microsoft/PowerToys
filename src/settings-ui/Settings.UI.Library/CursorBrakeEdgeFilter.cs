// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Which edges left at Default resist the cursor, as <c>edge_filter</c> stores it and the
    /// Apply to box lists it.
    /// </summary>
    public enum CursorBrakeEdgeFilter
    {
        All = 0,
        SideBySide = 1,
        Stacked = 2,
    }
}
