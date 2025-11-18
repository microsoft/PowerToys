// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Registry.Enumerations;

/// <summary>
/// The truncate side for a to long text
/// </summary>
internal enum TruncateSide
{
    /// <summary>
    /// Truncate a text only from the right side
    /// </summary>
    OnlyFromLeft,

    /// <summary>
    /// Truncate a text only from the left side
    /// </summary>
    OnlyFromRight,
}
