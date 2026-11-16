// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// The kind of value a robocopy switch accepts.
    /// </summary>
    public enum RobocopyOptionKind
    {
        /// <summary>A switch with no value, e.g. /E.</summary>
        Flag,

        /// <summary>A switch taking an integer, e.g. /R:3.</summary>
        Number,

        /// <summary>A switch taking an integer plus a storage unit, e.g. /IoMaxSize:64M.</summary>
        Storage,

        /// <summary>A switch taking free-form text, e.g. /XF:*.tmp.</summary>
        Text,

        /// <summary>A switch taking a set of single-letter flags, e.g. /COPY:DAT.</summary>
        MultiSelect,

        /// <summary>A switch taking an hhmm-hhmm run window, e.g. /RH:0100-0500.</summary>
        RunHours,
    }
}
