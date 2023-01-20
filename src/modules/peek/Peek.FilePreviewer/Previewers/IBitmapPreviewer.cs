// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Peek.FilePreviewer.Previewers
{
    public interface IBitmapPreviewer : IPreviewer
    {
        public BitmapSource? Preview { get; }

        public Stretch ImageStretch { get; set; }
    }
}
