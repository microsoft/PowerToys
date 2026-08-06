// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Awake.Core.Native;

public static class SessionStateDetector
{
    public static bool IsWorkstationLocked()
    {
        var hDesktop = Bridge.OpenInputDesktop(0, false, Constants.DESKTOP_SWITCHDESKTOP);

        if (hDesktop == IntPtr.Zero)
        {
            // Cannot open the input desktop => secure desktop (lock screen) is active
            return true;
        }

        try
        {
            // If we cannot switch to the input desktop, the session is locked
            return !Bridge.SwitchDesktop(hDesktop);
        }
        finally
        {
            Bridge.CloseDesktop(hDesktop);
        }
    }
}
