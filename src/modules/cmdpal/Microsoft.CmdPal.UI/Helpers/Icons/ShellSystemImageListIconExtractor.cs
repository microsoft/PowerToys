// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.UI.Helpers;

internal static partial class ShellSystemImageListIconExtractor
{
    private const int IldTransparent = 0x00000001;
    private static readonly Guid IidIImageList = new("46EB5926-582E-4017-9FDF-E8998DAA0950");

    public static ShellIconExtractionResult Extract(
        int imageIndex,
        bool jumbo,
        int requestedPixelSize)
    {
        var preferredSize = jumbo ? ShellImageListSize.Jumbo : ShellImageListSize.Large;
        var result = ExtractFromList(imageIndex, preferredSize, requestedPixelSize);
        if (result.HasContent || !jumbo)
        {
            return result;
        }

        result.Dispose();

        // Preserve ThumbnailHelper's existing jumbo behavior: if the 256-pixel
        // image-list entry is unavailable, retry with the normal large Shell icon.
        return ExtractFromList(imageIndex, ShellImageListSize.Large, requestedPixelSize);
    }

    private static ShellIconExtractionResult ExtractFromList(
        int imageIndex,
        ShellImageListSize imageListSize,
        int requestedPixelSize)
    {
        var imageListId = IidIImageList;
        nint imageList = 0;
        try
        {
            var result = NativeMethods.SHGetImageList(
                (int)imageListSize,
                ref imageListId,
                out imageList);
            if (result < 0 || imageList == 0)
            {
                return ShellIconExtractionResult.Empty(imageListSize, requestedPixelSize);
            }

            var sourceWidth = 0;
            var sourceHeight = 0;
            _ = NativeMethods.ImageList_GetIconSize(imageList, out sourceWidth, out sourceHeight);

            var iconHandle = NativeMethods.ImageList_GetIcon(imageList, imageIndex, IldTransparent);
            if (iconHandle == 0)
            {
                return ShellIconExtractionResult.Empty(
                    imageListSize,
                    requestedPixelSize,
                    sourceWidth,
                    sourceHeight);
            }

            var conversionStartedAt = Stopwatch.GetTimestamp();
            var softwareBitmap = HIconSoftwareBitmapConverter.ConvertAndDestroy(iconHandle);
            var conversionTicks = Stopwatch.GetTimestamp() - conversionStartedAt;
            if (softwareBitmap is null)
            {
                return ShellIconExtractionResult.Empty(
                    imageListSize,
                    requestedPixelSize,
                    sourceWidth,
                    sourceHeight,
                    conversionTicks);
            }

            if (sourceWidth <= 0 || sourceHeight <= 0)
            {
                sourceWidth = softwareBitmap.PixelWidth;
                sourceHeight = softwareBitmap.PixelHeight;
            }

            return ShellIconExtractionResult.FromSoftwareBitmap(
                softwareBitmap,
                imageListSize,
                requestedPixelSize,
                sourceWidth,
                sourceHeight,
                conversionTicks);
        }
        finally
        {
            if (imageList != 0)
            {
                _ = Marshal.Release(imageList);
            }
        }
    }

    private static partial class NativeMethods
    {
        [LibraryImport("shell32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial int SHGetImageList(
            int imageList,
            ref Guid interfaceId,
            out nint imageListPointer);

        [LibraryImport("comctl32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial nint ImageList_GetIcon(nint imageList, int imageIndex, int flags);

        [LibraryImport("comctl32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool ImageList_GetIconSize(
            nint imageList,
            out int width,
            out int height);
    }
}
