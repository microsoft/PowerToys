// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed partial class WindowHelper
{
    public static bool IsWindowFullscreen()
    {
        UserNotificationState state;

        // https://learn.microsoft.com/en-us/windows/win32/api/shellapi/ne-shellapi-query_user_notification_state
        if (Marshal.GetExceptionForHR(NativeMethods.SHQueryUserNotificationState(out state)) is null)
        {
            if (state == UserNotificationState.QUNS_RUNNING_D3D_FULL_SCREEN ||
                state == UserNotificationState.QUNS_BUSY ||
                state == UserNotificationState.QUNS_PRESENTATION_MODE)
            {
                return true;
            }
        }

        return false;
    }
}
