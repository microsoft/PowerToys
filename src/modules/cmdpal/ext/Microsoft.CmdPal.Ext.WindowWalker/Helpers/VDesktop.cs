// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers;

/// <summary>
/// Class that represents a Virtual Desktop
/// </summary>
/// <remarks>This class is named VDesktop to make clear it isn't an instance of the original Desktop class from Virtual Desktop Manager.
/// We can't use the original one, because therefore we must access private com interfaces. We aren't allowed to do this, because this is an official Microsoft project.</remarks>
public class VDesktop
{
    /// <summary>
    /// Gets or sets the guid of the desktop
    /// </summary>
    public Guid Id
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets the name of the desktop
    /// </summary>
    public string? Name
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets the number (position) of the desktop
    /// </summary>
    public int Number
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the desktop is currently visible to the user
    /// </summary>
    public bool IsVisible
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the desktop guid belongs to the generic "AllDesktops" view.
    /// This view hold all windows that are pinned to all desktops.
    /// </summary>
    public bool IsAllDesktopsView
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets a value indicating the position of a desktop in the list of all desktops
    /// </summary>
    public VirtualDesktopPosition Position
    {
        get; set;
    }

    /// <summary>
    /// Gets an empty instance of <see cref="VDesktop"/>
    /// </summary>
    public static VDesktop Empty => new()
    {
        Id = Guid.Empty,
        Name = string.Empty,
        Number = 0,
        IsVisible = true, // Setting this always to true to simulate a visible desktop
        IsAllDesktopsView = false,
        Position = VirtualDesktopPosition.NotApplicable,
    };
}
