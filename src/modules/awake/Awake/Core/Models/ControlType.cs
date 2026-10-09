// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Awake.Core.Models
{
    /// <summary>
    /// The type of control signal received by the handler.
    /// </summary>
    /// <remarks>
    /// See <see href="https://learn.microsoft.com/windows/console/handlerroutine">HandlerRoutine callback function</see>.
    /// </remarks>
    public enum ControlType
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT = 1,
        CTRL_CLOSE_EVENT = 2,
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT = 6,
    }
}
