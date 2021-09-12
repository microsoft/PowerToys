// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Media.Imaging;

namespace ColorPicker.ViewModelContracts
{
    public interface IZoomViewModel
    {
        BitmapSource ZoomArea { get; set; }

        double ZoomFactor { get; set; }

        double DesiredWidth { get; set; }

        double DesiredHeight { get; set; }

        double Width { get; set; }

        double Height { get; set; }
    }
}
