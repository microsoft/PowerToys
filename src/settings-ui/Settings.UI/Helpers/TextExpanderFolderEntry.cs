// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// One entry of a folder listing, as the listing itself describes it, without opening the file.
    /// <c>IsLink</c> says whether a folder is a symbolic link or junction. Every OneDrive folder is a
    /// reparse point, a cloud placeholder, but not a link.
    /// </summary>
    public readonly record struct TextExpanderFolderEntry(string Name, bool IsDirectory, FileAttributes Attributes, bool IsLink, DateTime LastWriteTime);
}
