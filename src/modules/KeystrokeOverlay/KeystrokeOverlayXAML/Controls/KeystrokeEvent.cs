// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace KeystrokeOverlayUI.Controls
{
    [StructLayout(LayoutKind.Sequential)]
    public struct KeystrokeEvent
    {
        public uint VirtualKey;   // Matches typical DWORD
        public bool IsPressed;    // Matches bool (check if C++ uses BOOL or bool)
    }
}
