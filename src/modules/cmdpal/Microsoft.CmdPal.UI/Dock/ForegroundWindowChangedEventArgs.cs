// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Dock;

/// <summary>
/// Describes a foreground-window change reported by <see cref="WindowsEventsListener"/>.
/// </summary>
internal sealed class ForegroundWindowChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets a value indicating whether the foreground window is recognized as the desktop.
    /// </summary>
    public bool IsDesktopWindow { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForegroundWindowChangedEventArgs"/> class.
    /// </summary>
    /// <param name="isDesktopWindow">Whether the foreground window is recognized as the desktop.</param>
    public ForegroundWindowChangedEventArgs(bool isDesktopWindow)
    {
        IsDesktopWindow = isDesktopWindow;
    }
}
