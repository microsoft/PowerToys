// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Awake.Core.Models
{
    internal enum TrayCommands : uint
    {
        TC_DISPLAY_SETTING = Native.Constants.WM_USER + 1,
        TC_MODE_PASSIVE = Native.Constants.WM_USER + 2,
        TC_MODE_INDEFINITE = Native.Constants.WM_USER + 3,
        TC_MODE_EXPIRABLE = Native.Constants.WM_USER + 4,
        TC_EXIT = Native.Constants.WM_USER + 100,
        TC_TIME = Native.Constants.WM_USER + 101,
    }
}
