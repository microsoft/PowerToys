// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ShowDesktop
{
    internal sealed class MouseHookEventArgs
    {
        public int X { get; init; }

        public int Y { get; init; }

        public bool IsDoubleClick { get; init; }

        public bool IsTaskbar { get; init; }
    }
}
