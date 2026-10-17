// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Apps;

/// <summary>Controls when exact executable names receive extra search priority.</summary>
public enum ExecutableNameMatchMode
{
    /// <summary>Use ordinary search scoring without exact executable-name priority.</summary>
    Disabled,

    /// <summary>Prioritize only the complete executable filename, including its extension.</summary>
    FilenameOnly,

    /// <summary>Prioritize the complete executable filename or its extensionless name.</summary>
    FilenameAndStem,
}
