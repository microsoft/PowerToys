// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Xaml.Media;
using Peek.Common;
using Peek.Common.Models;

namespace Peek.FilePreviewer.Previewers.Helpers;

/// <summary>
/// Create thumbnail or icon images for a file, or retrieve them from the Windows Explorer thumbnail and icon caches.
/// </summary>
/// <remarks>Inspired by https://stackoverflow.com/questions/21751747/extract-thumbnail-for-any-file-in-windows</remarks>
public static class ThumbnailHelper
{
    // The maximum size Explorer's thumbnail cache currently supports. Used for cache retrieval.
    private static readonly NativeSize MaxThumbnailSize = new(2560, 2560);

    // Default size for all previewers except ImagePreviewer.
    private static readonly NativeSize DefaultThumbnailSize = new(256, 256);

    // Used to retrieve the Shell Item for a given Windows Explorer resource, so its thumbnail/icon can be retrieved.
    private static Guid _shellItem2Guid = new("7E9FB0D3-919F-4307-AB2E-9B1860310C93");

    /// <summary>
    /// Get a file's icon bitmap.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <param name="cancellationToken">The async task cancellation token.</param>
    /// <returns>An <see cref="ImageSource"/> corresponding to the file's icon bitmap, or null if retrieval failed.</returns>
    /// <remarks>If a cached icon cannot be found, a new icon will be created and added to the Explorer icon cache.</remarks>
    public static async Task<ImageSource?> GetIconAsync(string path, CancellationToken cancellationToken)
    {
        return await GetImageAsync(path, ThumbnailOptions.BiggerSizeOk | ThumbnailOptions.IconOnly, true, cancellationToken);
    }

    /// <summary>
    /// Get a file's thumbnail bitmap.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <param name="cancellationToken">The async task cancellation token.</param>
    /// <returns>An <see cref="ImageSource"/> corresponding to the file's thumbnail bitmap, or null if retrieval failed.</returns>
    /// <remarks>If a cached thumbnail cannot be found, a new thumbnail will be created and added to the Explorer thumbnail cache.</remarks>
    public static async Task<ImageSource?> GetThumbnailAsync(string path, CancellationToken cancellationToken)
    {
        return await GetImageAsync(path, ThumbnailOptions.BiggerSizeOk | ThumbnailOptions.ThumbnailOnly, true, cancellationToken);
    }

    /// <summary>
    /// Get the highest-resolution image available for a file from the Explorer thumbnail and icon caches.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <param name="supportsTransparency">Whether the file's type supports transparency. Set to true for PNG image files.</param>
    /// <param name="cancellationToken">The async task cancellation token.</param>
    /// <returns>An <see cref="ImageSource"/> corresponding to the thumbnail or icon, or null if there is no icon or thumbnail cached for the file.</returns>
    public static async Task<ImageSource?> GetCachedThumbnailAsync(string path, bool supportsTransparency, CancellationToken cancellationToken)
    {
        return await GetImageAsync(path, ThumbnailOptions.InCacheOnly, supportsTransparency, cancellationToken);
    }

    private static async Task<ImageSource?> GetImageAsync(string path, ThumbnailOptions options, bool supportsTransparency, CancellationToken cancellationToken)
    {
        IntPtr hBitmap = IntPtr.Zero;
        IShellItem? nativeShellItem = null;
        bool checkCacheOnly = options.HasFlag(ThumbnailOptions.InCacheOnly);

        try
        {
            int retCode = NativeMethods.SHCreateItemFromParsingName(path, IntPtr.Zero, ref _shellItem2Guid, out nativeShellItem);
            if (retCode != 0)
            {
                throw Marshal.GetExceptionForHR(retCode)!;
            }

            cancellationToken.ThrowIfCancellationRequested();

            HResult hr = ((IShellItemImageFactory)nativeShellItem).GetImage(checkCacheOnly ? MaxThumbnailSize : DefaultThumbnailSize, options, out hBitmap);

            cancellationToken.ThrowIfCancellationRequested();

            return hr == HResult.Ok ? await BitmapHelper.GetBitmapFromHBitmapAsync(hBitmap, supportsTransparency, cancellationToken) : null;
        }
        finally
        {
            // Delete the unmanaged bitmap resource.
            if (hBitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(hBitmap);
            }

            if (nativeShellItem != null)
            {
                Marshal.ReleaseComObject(nativeShellItem);
            }
        }
    }
}
