// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Peek.FilePreviewer.Models;

namespace Peek.FilePreviewer.Previewers;

public interface ISpecialFolderPreviewer : IPreviewer
{
    public SpecialFolderPreviewData? Preview { get; }
}
