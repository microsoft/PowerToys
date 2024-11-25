// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/
using System;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;

namespace Microsoft.CmdPal.Ext.WindowWalker.Components;

/// <summary>
/// Class containing methods to control the live preview
/// </summary>
internal sealed class LivePreview
{
    /// <summary>
    /// Makes sure that a window is excluded from the live preview
    /// </summary>
    /// <param name="hwnd">handle to the window to exclude</param>
    public static void SetWindowExclusionFromLivePreview(IntPtr hwnd)
    {
        var renderPolicy = (uint)DwmNCRenderingPolicies.Enabled;

        _ = NativeMethods.DwmSetWindowAttribute(
            hwnd,
            12,
            ref renderPolicy,
            sizeof(uint));
    }

    /// <summary>
    /// Activates the live preview
    /// </summary>
    /// <param name="targetWindow">the window to show by making all other windows transparent</param>
    /// <param name="windowToSpare">the window which should not be transparent but is not the target window</param>
    public static void ActivateLivePreview(IntPtr targetWindow, IntPtr windowToSpare)
    {
        _ = NativeMethods.DwmpActivateLivePreview(
                true,
                targetWindow,
                windowToSpare,
                LivePreviewTrigger.Superbar,
                IntPtr.Zero);
    }

    /// <summary>
    /// Deactivates the live preview
    /// </summary>
    public static void DeactivateLivePreview()
    {
        _ = NativeMethods.DwmpActivateLivePreview(
                false,
                IntPtr.Zero,
                IntPtr.Zero,
                LivePreviewTrigger.AltTab,
                IntPtr.Zero);
    }
}
