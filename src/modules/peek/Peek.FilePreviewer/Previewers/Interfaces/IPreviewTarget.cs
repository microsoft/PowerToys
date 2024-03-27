// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Peek.Common.Models;

namespace Peek.FilePreviewer.Previewers.Interfaces
{
    public interface IPreviewTarget
    {
        static abstract bool IsItemSupported(IFileSystemItem item);
    }
}
