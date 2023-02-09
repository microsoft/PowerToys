// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static Peek.Common.Helpers.PropertyStoreHelper;

namespace Peek.Common.Models
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SHELLDETAILS
    {
        public int Fmt;
        public int CxChar;
        public Strret Str;
    }
}
