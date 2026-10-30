// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Apps.Programs;

/// <summary>Stores a stable catalog or legacy source identity for an application hidden by the user.</summary>
public class DisabledProgramSource
{
    /// <summary>Gets or sets the identity retained as the user's hidden preference.</summary>
    public string UniqueIdentifier { get; set; } = string.Empty;
}
