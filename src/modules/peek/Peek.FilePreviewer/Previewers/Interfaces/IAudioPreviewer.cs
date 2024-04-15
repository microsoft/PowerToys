// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Peek.FilePreviewer.Previewers.MediaPreviewer.Models;

namespace Peek.FilePreviewer.Previewers.Interfaces
{
    public interface IAudioPreviewer : IPreviewer, IPreviewTarget
    {
        public AudioPreviewData? Preview { get; }
    }
}
