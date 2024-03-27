// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Peek.FilePreviewer.Previewers.Interfaces
{
    public interface IBrowserPreviewer : IPreviewer, IPreviewTarget
    {
        public Uri? Preview { get; }

        public bool IsDevFilePreview { get; }
    }
}
