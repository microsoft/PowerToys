// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using Peek.FilePreviewer.Previewers.Archives.Models;

namespace Peek.FilePreviewer.Previewers.Interfaces
{
    public interface IArchivePreviewer : IPreviewer, IPreviewTarget, IDisposable
    {
        ObservableCollection<ArchiveItem> Tree { get; }

        string? DirectoryCountText { get; }

        string? FileCountText { get; }

        string? SizeText { get; }
    }
}
