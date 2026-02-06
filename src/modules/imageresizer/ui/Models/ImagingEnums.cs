// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ImageResizer.Models
{
    /// <summary>
    /// PNG interlace option for the encoder.
    /// Integer values preserve backward compatibility with existing settings JSON.
    /// </summary>
    public enum PngInterlaceOption
    {
        Default = 0,
        On = 1,
        Off = 2,
    }

    /// <summary>
    /// TIFF compression option for the encoder.
    /// Integer values preserve backward compatibility with existing settings JSON.
    /// </summary>
    public enum TiffCompressOption
    {
        Default = 0,
        None = 1,
        Ccitt3 = 2,
        Ccitt4 = 3,
        Lzw = 4,
        Rle = 5,
        Zip = 6,
    }
}
