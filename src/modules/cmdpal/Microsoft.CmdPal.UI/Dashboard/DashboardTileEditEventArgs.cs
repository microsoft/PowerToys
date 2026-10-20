// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Dashboard;

internal sealed class DashboardTileEditEventArgs(DashboardTileEditKind kind, Point delta) : EventArgs
{
    public DashboardTileEditKind Kind { get; } = kind;

    /// <summary>
    /// Gets the total distance the pointer has moved since the gesture started, in DIPs.
    /// </summary>
    public Point Delta { get; } = delta;
}
