// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/

using System;

namespace WindowWalker.Components
{
    /// <summary>
    /// Class containg methods to control the live preview
    /// </summary>
    internal class LivePreview
    {
        /// <summary>
        /// Makes sure that a window is excluded from the live preview
        /// </summary>
        /// <param name="hwnd">handle to the window to exclude</param>
        public static void SetWindowExlusionFromLivePreview(IntPtr hwnd)
        {
            int renderPolicy = (int)InteropAndHelpers.DwmNCRenderingPolicy.Enabled;

            InteropAndHelpers.DwmSetWindowAttribute(
                hwnd,
                12,
                ref renderPolicy,
                sizeof(int));
        }

        /// <summary>
        /// Activates the live preview
        /// </summary>
        /// <param name="targetWindow">the window to show by making all other windows transparent</param>
        /// <param name="windowToSpare">the window which should not be transparent but is not the target window</param>
        public static void ActivateLivePreview(IntPtr targetWindow, IntPtr windowToSpare)
        {
            InteropAndHelpers.DwmpActivateLivePreview(
                    true,
                    targetWindow,
                    windowToSpare,
                    InteropAndHelpers.LivePreviewTrigger.Superbar,
                    IntPtr.Zero);
        }

        /// <summary>
        /// Deactivates the live preview
        /// </summary>
        public static void DeactivateLivePreview()
        {
            InteropAndHelpers.DwmpActivateLivePreview(
                    false,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    InteropAndHelpers.LivePreviewTrigger.AltTab,
                    IntPtr.Zero);
        }
    }
}
