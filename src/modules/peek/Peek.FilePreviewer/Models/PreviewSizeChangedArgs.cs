// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Peek.FilePreviewer.Models
{
    public class PreviewSizeChangedArgs
    {
        public PreviewSizeChangedArgs(PreviewSize previewSize)
        {
            PreviewSize = previewSize;
        }

        public PreviewSize PreviewSize { get; init; }
    }
}
