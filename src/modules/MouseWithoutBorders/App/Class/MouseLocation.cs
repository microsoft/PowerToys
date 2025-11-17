// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseWithoutBorders.Class
{
    internal class MouseLocation
    {
        internal int X { get; set; }

        internal int Y { get; set; }

        internal int Count { get; set; }

        internal void ResetCount()
        {
            Count = 1;
        }
    }
}
