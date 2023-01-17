// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Peek.FilePreviewer.Models
{
    public enum SizeFormat
    {
        Pixels,
        Percentage,
    }

    public class PreviewSizeChangedArgs
    {
        public PreviewSizeChangedArgs(Size? windowSizeRequested, SizeFormat sizeFormat = SizeFormat.Pixels)
        {
            WindowSizeRequested = windowSizeRequested;
            WindowSizeFormat = sizeFormat;
        }

        public Size? WindowSizeRequested { get; init; }

        public SizeFormat WindowSizeFormat { get; init; }
    }
}
