// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers;

public static class OSVersionHelper
{
    public static bool IsWindows11()
    {
        return Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000;
    }

    public static bool IsGreaterThanWindows11_21H2()
    {
        return Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build > 22000;
    }
}
