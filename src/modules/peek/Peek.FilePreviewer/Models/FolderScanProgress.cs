// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Models
{
    public readonly record struct FolderScanProgress(ulong TotalBytes, ulong FileCount, ulong DirectoryCount, FolderScanState State);
}
