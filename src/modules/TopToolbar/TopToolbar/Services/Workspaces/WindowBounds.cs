// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace TopToolbar.Services.Workspaces
{
    internal readonly struct WindowBounds
    {
        public WindowBounds(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Left { get; }

        public int Top { get; }

        public int Right { get; }

        public int Bottom { get; }

        public int Width => Right - Left;

        public int Height => Bottom - Top;

        public bool IsEmpty => Width <= 0 || Height <= 0;
    }
}
