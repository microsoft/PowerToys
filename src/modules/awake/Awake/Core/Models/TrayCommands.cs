// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32;

namespace Awake.Core.Models
{
    internal enum TrayCommands : uint
    {
        TC_DISPLAY_SETTING = PInvoke.WM_USER + 1,
        TC_MODE_PASSIVE = PInvoke.WM_USER + 2,
        TC_MODE_INDEFINITE = PInvoke.WM_USER + 3,
        TC_MODE_EXPIRABLE = PInvoke.WM_USER + 4,
        TC_EXIT = PInvoke.WM_USER + 100,
        TC_TIME = PInvoke.WM_USER + 101,
    }
}
