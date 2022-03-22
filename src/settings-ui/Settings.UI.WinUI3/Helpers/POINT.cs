// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.Settings.UI.WinUI3.Helpers
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Interop")]
    public struct POINT
    {
        public int X { get; set; }

        public int Y { get; set; }

        public POINT(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
