// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Apps.Win32;

/// <summary>Classifies Win32 launch targets; member names must remain stable for serialized catalog payloads.</summary>
internal enum Win32AppType
{
    WebApplication = 0,
    InternetShortcutApplication = 1,
    Win32Application = 2,
    ShortcutApplication = 3,
    ApprefApplication = 4,
    RunCommand = 5,
    Folder = 6,
    GenericFile = 7,
}
