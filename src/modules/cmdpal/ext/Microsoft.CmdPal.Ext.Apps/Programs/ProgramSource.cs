// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Apps.Programs;

/// <summary>
/// Contains user added folder location contents as well as all user disabled applications
/// </summary>
/// <remarks>
/// <para>Win32 class applications set UniqueIdentifier using their full file path</para>
/// <para>UWP class applications set UniqueIdentifier using their Application User Model ID</para>
/// <para>Custom user added program sources set UniqueIdentifier using their location</para>
/// </remarks>
public class ProgramSource
{
    public string Location { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public string UniqueIdentifier { get; set; } = string.Empty;
}
