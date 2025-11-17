// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Peek.Common.WIC
{
    [Flags]
    public enum StreamAccessMode : int
    {
        GENERIC_WRITE = 0x40000000,
        GENERIC_READ = unchecked((int)0x80000000U),
    }
}
