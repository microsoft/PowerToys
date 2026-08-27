// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

internal readonly record struct ShellItemIconRequest(
    string CacheIdentity,
    string ItemPath,
    bool Jumbo)
{
    public ShellItemIconRequest(string itemPath, bool jumbo)
        : this(itemPath, itemPath, jumbo)
    {
    }
}
