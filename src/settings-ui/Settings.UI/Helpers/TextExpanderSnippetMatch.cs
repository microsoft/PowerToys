// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>A snippet file the OneDrive search found, in the account folder <c>Root</c>, as its folder's listing described it.</summary>
    public sealed record TextExpanderSnippetMatch(string Root, string FilePath, DateTime LastWriteTime);
}
