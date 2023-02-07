// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Peek.Common.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Blob
    {
        public int CbSize;
        public IntPtr PBlobData;
    }
}
