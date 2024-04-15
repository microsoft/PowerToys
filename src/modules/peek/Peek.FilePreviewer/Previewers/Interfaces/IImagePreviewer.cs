// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Peek.FilePreviewer.Previewers.Interfaces
{
    public interface IImagePreviewer : IPreviewer, IPreviewTarget
    {
        public ImageSource? Preview { get; }

        public double ScalingFactor { get; set; }

        public Size MaxImageSize { get; set; }
    }
}
