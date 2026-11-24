// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Graphics;

namespace WorkspacesLauncherUI.Helpers
{
    internal static class WindowPlacement
    {
        // All rectangles and sizes use physical screen coordinates.
        internal static RectInt32 GetCenteredBounds(RectInt32 workArea, SizeInt32 desiredSize, RectInt32? ownerBounds = null)
        {
            var width = Math.Min(desiredSize.Width, workArea.Width);
            var height = Math.Min(desiredSize.Height, workArea.Height);
            var centerBounds = ownerBounds ?? workArea;
            var x = centerBounds.X + ((centerBounds.Width - width) / 2);
            var y = centerBounds.Y + ((centerBounds.Height - height) / 2);
            x = Math.Clamp(x, workArea.X, workArea.X + workArea.Width - width);
            y = Math.Clamp(y, workArea.Y, workArea.Y + workArea.Height - height);
            return new RectInt32(x, y, width, height);
        }
    }
}
