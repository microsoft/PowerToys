// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Creates canonical application identities whose format is shared across discovery sources.
/// </summary>
internal static class AppIdentity
{
    private const string PackagedPrefix = "packaged:";

    /// <summary>Creates the stable catalog identity for a packaged application.</summary>
    internal static string ForPackaged(string aumid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aumid);
        return $"{PackagedPrefix}{aumid}";
    }
}
