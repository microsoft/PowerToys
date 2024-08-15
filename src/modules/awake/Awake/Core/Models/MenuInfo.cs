// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Awake.Core.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MenuInfo
    {
        public uint CbSize;             // Size of the structure, in bytes
        public uint FMask;              // Specifies which members of the structure are valid
        public uint DwStyle;            // Style of the menu
        public uint CyMax;              // Maximum height of the menu, in pixels
        public IntPtr HbrBack;          // Handle to the brush used for the menu's background
        public uint DwContextHelpID;    // Context help ID
        public IntPtr DwMenuData;       // Pointer to the menu's user data
    }
}
