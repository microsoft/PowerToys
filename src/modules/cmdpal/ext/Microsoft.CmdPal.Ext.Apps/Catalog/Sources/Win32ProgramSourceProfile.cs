// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

/// <summary>
/// Describes how one Win32 discovery origin scans and interprets the paths it contributes.
/// </summary>
[Flags]
internal enum Win32ProgramSourceProfile
{
    /// <summary>Uses shortcut-style discovery without additional allowances.</summary>
    None = 0,

    /// <summary>Allows generic files and folders as application-list entries.</summary>
    IncludeNonApplications = 1 << 0,

    /// <summary>Allows executable paths to be indexed directly instead of requiring a shortcut representation.</summary>
    IncludeRawExecutables = 1 << 1,

    /// <summary>Loads executable paths as direct run commands.</summary>
    LoadAsRunCommand = 1 << 2,

    /// <summary>Permits scanning and watching descendants below each configured directory.</summary>
    RecurseSubdirectories = 1 << 3,
}
