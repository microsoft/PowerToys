// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Peek.UI.Helpers
{
    /// <summary>
    /// Orders monitor relocation before final window sizing so DPI-related resizing
    /// during a move does not overwrite the intended physical-pixel bounds.
    /// </summary>
    /// <remarks>
    /// Native placement is supplied by the caller, allowing the ordering to be tested
    /// without a window.
    /// </remarks>
    internal static class WindowPlacementHelper
    {
        /// <summary>
        /// Performs any DPI-changing relocation before applying the final bounds.
        /// </summary>
        /// <param name="dpiChanges">Whether moving to the target monitor changes the
        /// window's DPI.</param>
        /// <param name="moveToMonitor">Relocates the window without requesting a new
        /// size.</param>
        /// <param name="resize">Applies the final physical-pixel position and size after
        /// relocation.</param>
        public static void Apply(bool dpiChanges, Action moveToMonitor, Action resize)
        {
            if (dpiChanges)
            {
                // WinUI may resize the window while processing a DPI-changing move.
                // Complete that transition before committing final physical-pixel bounds.
                moveToMonitor();
            }

            resize();
        }
    }
}
