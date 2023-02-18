// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Media.Imaging;

namespace Peek.FilePreviewer.Previewers
{
    public interface ISvgPreviewer : IPreviewer
    {
        public SvgImageSource? Preview { get; }

        public double ScalingFactor { get; set; }
    }
}
