// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Microsoft.CmdPal.UI.Taskbar;

/// <summary>
/// Represents a button on the Windows taskbar.
/// </summary>
public sealed record TasklistButton
{
    /// <summary>
    /// Gets the name/automation ID of the taskbar button.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the X coordinate of the button.
    /// </summary>
    public int X { get; init; }

    /// <summary>
    /// Gets the Y coordinate of the button.
    /// </summary>
    public int Y { get; init; }

    /// <summary>
    /// Gets the width of the button.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets the height of the button.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Gets the assigned key number for the button (1-10).
    /// </summary>
    public int KeyNum { get; init; }
}
