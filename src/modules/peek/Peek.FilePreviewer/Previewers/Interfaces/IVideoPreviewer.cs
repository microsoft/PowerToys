// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Media.Core;

namespace Peek.FilePreviewer.Previewers.Interfaces
{
    public interface IVideoPreviewer : IPreviewer, IPreviewTarget
    {
        public MediaSource? Preview { get; }
    }
}
