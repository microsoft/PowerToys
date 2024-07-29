// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Awake.Core.Models
{
    internal enum TrayCommands : uint
    {
        TC_DISPLAY_SETTING = Native.Constants.WM_USER + 0x2,
        TC_MODE_PASSIVE = Native.Constants.WM_USER + 0x3,
        TC_MODE_INDEFINITE = Native.Constants.WM_USER + 0x4,
        TC_MODE_EXPIRABLE = Native.Constants.WM_USER + 0x5,
        TC_EXIT = Native.Constants.WM_USER + 0x64,
        TC_TIME = Native.Constants.WM_USER + 0x65,
    }
}
