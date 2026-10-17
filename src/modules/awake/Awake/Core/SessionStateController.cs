// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Awake.Core;

[SupportedOSPlatform("windows")]
internal static class SessionStateController
{
    internal static bool InitializeLockState(Func<bool> getCurrentState)
    {
        return getCurrentState();
    }

    internal static bool ApplySessionSwitch(SessionSwitchReason reason, bool currentState)
    {
        return reason switch
        {
            SessionSwitchReason.SessionLock => true,
            SessionSwitchReason.SessionUnlock => false,
            _ => currentState,
        };
    }
}
