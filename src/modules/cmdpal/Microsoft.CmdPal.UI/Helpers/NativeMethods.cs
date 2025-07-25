// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Security;

namespace Microsoft.CmdPal.UI.Helpers;

[SuppressUnmanagedCodeSecurity]
internal static class NativeMethods
{
    [DllImport("shell32.dll")]
    public static extern int SHQueryUserNotificationState(out UserNotificationState state);
}

internal enum UserNotificationState : int
{
    QUNS_NOT_PRESENT = 1,
    QUNS_BUSY,
    QUNS_RUNNING_D3D_FULL_SCREEN,
    QUNS_PRESENTATION_MODE,
    QUNS_ACCEPTS_NOTIFICATIONS,
    QUNS_QUIET_TIME,
    QUNS_APP,
}
