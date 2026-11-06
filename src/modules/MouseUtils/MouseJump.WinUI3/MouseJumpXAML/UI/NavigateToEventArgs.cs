// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using MouseJump.Models.Display;
using MouseJump.Models.Drawing;

namespace MouseJump.WinUI3.UI;

/// <summary>
/// Implements the NavigateTo event that is used by the PreviewWindow to signal
/// to the hosting application that the user wants to move the cursor to a specific
/// location. Can be raised as the result of a mouse click on the preview window,
/// or a keyboard shortcut that means the same thing (e.g. 1-9, arrow keys, P, Home, End)
/// </summary>
public sealed class NavigateToEventArgs : EventArgs
{
    public NavigateToEventArgs(DeviceInfo device, PointInfo location)
    {
        this.Device = device ?? throw new ArgumentNullException(nameof(device));
        this.Location = location ?? throw new ArgumentNullException(nameof(location));
    }

    /// <summary>
    /// Gets the device the target screen belongs to - lets the host distinguish a local device
    /// (move the cursor directly) from a remote one (route the move elsewhere) once remote
    /// devices are supported.
    /// </summary>
    public DeviceInfo Device
    {
        get;
    }

    /// <summary>
    /// Gets the physical desktop location the target screen maps onto - already resolved
    /// against that screen's own display area, ready to pass to
    /// <c>MouseHelper.SetCursorPosition</c>.
    /// </summary>
    public PointInfo Location
    {
        get;
    }
}
