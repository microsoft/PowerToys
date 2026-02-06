// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Graphics.Imaging;

namespace ImageResizer.Utilities
{
    /// <summary>
    /// Maps between legacy container format GUIDs (used in settings JSON) and WinRT encoder/decoder IDs,
    /// and provides file extension lookups.
    /// </summary>
    internal static class CodecHelper
    {
        // Legacy container format GUID (stored in settings JSON) -> WinRT Encoder ID
        private static readonly Dictionary<Guid, Guid> LegacyGuidToEncoderId = new()
        {
            [new Guid("19e4a5aa-5662-4fc5-a0c0-1758028e1057")] = BitmapEncoder.JpegEncoderId,
            [new Guid("1b7cfaf4-713f-473c-bbcd-6137425faeaf")] = BitmapEncoder.PngEncoderId,
            [new Guid("0af1d87e-fcfe-4188-bdeb-a7906471cbe3")] = BitmapEncoder.BmpEncoderId,
            [new Guid("163bcc30-e2e9-4f0b-961d-a3e9fdb788a3")] = BitmapEncoder.TiffEncoderId,
            [new Guid("1f8a5601-7d4d-4cbd-9c82-1bc8d4eeb9a5")] = BitmapEncoder.GifEncoderId,
        };

        // WinRT Decoder ID -> WinRT Encoder ID
        private static readonly Dictionary<Guid, Guid> DecoderIdToEncoderId = new()
        {
            [BitmapDecoder.JpegDecoderId] = BitmapEncoder.JpegEncoderId,
            [BitmapDecoder.PngDecoderId] = BitmapEncoder.PngEncoderId,
            [BitmapDecoder.BmpDecoderId] = BitmapEncoder.BmpEncoderId,
            [BitmapDecoder.TiffDecoderId] = BitmapEncoder.TiffEncoderId,
            [BitmapDecoder.GifDecoderId] = BitmapEncoder.GifEncoderId,
            [BitmapDecoder.JpegXRDecoderId] = BitmapEncoder.JpegXREncoderId,
        };

        // Encoder ID -> supported file extensions
        private static readonly Dictionary<Guid, string[]> EncoderExtensions = new()
        {
            [BitmapEncoder.JpegEncoderId] = new[] { ".jpg", ".jpeg", ".jpe", ".jfif" },
            [BitmapEncoder.PngEncoderId] = new[] { ".png" },
            [BitmapEncoder.BmpEncoderId] = new[] { ".bmp", ".dib", ".rle" },
            [BitmapEncoder.TiffEncoderId] = new[] { ".tiff", ".tif" },
            [BitmapEncoder.GifEncoderId] = new[] { ".gif" },
            [BitmapEncoder.JpegXREncoderId] = new[] { ".jxr", ".wdp" },
        };

        /// <summary>
        /// Gets the WinRT encoder ID that corresponds to the given legacy container format GUID.
        /// Falls back to PNG if the GUID is not recognized.
        /// </summary>
        public static Guid GetEncoderIdFromLegacyGuid(Guid containerFormatGuid)
            => LegacyGuidToEncoderId.TryGetValue(containerFormatGuid, out var id)
                ? id
                : BitmapEncoder.PngEncoderId;

        /// <summary>
        /// Gets the WinRT encoder ID that matches the given decoder's codec.
        /// Returns null if no matching encoder exists (e.g., ICO decoder has no encoder).
        /// </summary>
        public static Guid? GetEncoderIdForDecoder(BitmapDecoder decoder)
        {
            var codecId = decoder.DecoderInformation?.CodecId ?? Guid.Empty;
            return DecoderIdToEncoderId.TryGetValue(codecId, out var encoderId)
                ? encoderId
                : null;
        }

        /// <summary>
        /// Returns the supported file extensions for the given encoder ID.
        /// </summary>
        public static string[] GetSupportedExtensions(Guid encoderId)
            => EncoderExtensions.TryGetValue(encoderId, out var extensions)
                ? extensions
                : Array.Empty<string>();

        /// <summary>
        /// Returns the default (first) file extension for the given encoder ID.
        /// </summary>
        public static string GetDefaultExtension(Guid encoderId)
            => GetSupportedExtensions(encoderId).FirstOrDefault() ?? ".png";

        /// <summary>
        /// Checks whether the given encoder ID is a known, supported encoder.
        /// </summary>
        public static bool CanEncode(Guid encoderId)
            => EncoderExtensions.ContainsKey(encoderId);
    }
}
