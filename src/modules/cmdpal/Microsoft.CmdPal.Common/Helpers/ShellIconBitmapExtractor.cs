// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices.WindowsRuntime;
using ManagedCommon;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using Wic = Windows.Win32.Graphics.Imaging;

namespace Microsoft.CmdPal.Common.Helpers;

public static class ShellIconBitmapExtractor
{
    private const int DefaultIconSize = 256;

    public static async Task<IRandomAccessStream?> GetIconStreamAsync(string iconReference, int size)
    {
        var bitmap = await Task.Run(() => GetIconBitmap(iconReference, size)).ConfigureAwait(false);
        if (bitmap == null)
        {
            return null;
        }

        try
        {
            var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream).AsTask().ConfigureAwait(false);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync().AsTask().ConfigureAwait(false);

            stream.Seek(0);
            return stream;
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    public static SoftwareBitmap? GetIconBitmap(string iconReference, int size)
    {
        if (string.IsNullOrWhiteSpace(iconReference))
        {
            return null;
        }

        var targetSize = Math.Max(size, 1);
        var parsed = IconStringParser.Parse(iconReference);

        try
        {
            if (parsed.Kind is IconStringKind.ShellIcon &&
                !string.IsNullOrEmpty(parsed.BinaryPath))
            {
                return GetBinaryIconBitmap(parsed.BinaryPath, parsed.BinaryIconIndex, targetSize);
            }

            return TryGetShellItemPath(iconReference, out var shellPath)
                ? GetShellItemBitmap(shellPath, targetSize)
                : null;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load icon bitmap from '{iconReference}' at size {targetSize}", ex);
            return null;
        }
    }

    public static SoftwareBitmap? GetBinaryIconBitmap(string binaryPath, int iconIndex, int size)
    {
        var targetSize = Math.Max(size, 1);
        var resolvedPath = ResolveBinaryPath(binaryPath);

        if (TryExtractIconBitmap(resolvedPath, iconIndex, targetSize, preferExactRequestedSize: true, out var bitmap))
        {
            return bitmap;
        }

        if (targetSize < DefaultIconSize &&
            TryExtractIconBitmap(resolvedPath, iconIndex, DefaultIconSize, preferExactRequestedSize: false, out bitmap))
        {
            return bitmap;
        }

        // SHDefExtractIconW can return S_FALSE for .lnk files and other
        // shell items where the icon isn't embedded at the given index.
        // Fall back to IShellItemImageFactory which follows the full
        // shell icon resolution chain (shortcut targets, overlays, etc.).
        return GetShellItemBitmap(resolvedPath, targetSize);
    }

    private static unsafe SoftwareBitmap? GetShellItemBitmap(string path, int size)
    {
        IShellItemImageFactory* factory = null;
        HBITMAP hBitmap = default;

        try
        {
            fixed (char* pPath = path)
            {
                var iid = IShellItemImageFactory.IID_Guid;
                var hr = PInvoke.SHCreateItemFromParsingName(
                    pPath,
                    null,
                    &iid,
                    (void**)&factory);

                if (hr.Failed || factory == null)
                {
                    Logger.LogWarning($"SHCreateItemFromParsingName failed for path='{path}', size={size}, HRESULT=0x{hr.Value:X8}.");
                    return null;
                }
            }

            var requestedSize = new SIZE { cx = size, cy = size };
            try
            {
                factory->GetImage(
                    requestedSize,
                    SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_BIGGERSIZEOK | SIIGBF.SIIGBF_CROPTOSQUARE,
                    &hBitmap);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"IShellItemImageFactory.GetImage failed for path='{path}', size={size}: {ex}");
                return null;
            }

            if (hBitmap.IsNull)
            {
                Logger.LogWarning($"IShellItemImageFactory.GetImage returned a null bitmap for path='{path}', size={size}.");
                return null;
            }

            return CreateSoftwareBitmapFromBitmapHandle(hBitmap);
        }
        finally
        {
            if (!hBitmap.IsNull)
            {
                PInvoke.DeleteObject(hBitmap);
            }

            if (factory != null)
            {
                factory->Release();
            }
        }
    }

    private static unsafe SoftwareBitmap? CreateSoftwareBitmapFromIcon(HICON iconHandle)
    {
        const string FailureContext = "binary icon reference";
        Wic.IWICBitmap* wicBitmap = null;

        if (!TryCreateImagingFactory(out var imagingFactory, FailureContext))
        {
            return null;
        }

        try
        {
            try
            {
                imagingFactory->CreateBitmapFromHICON(iconHandle, &wicBitmap);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"IWICImagingFactory.CreateBitmapFromHICON failed while converting a {FailureContext}: {ex}");
                return null;
            }

            if (wicBitmap == null)
            {
                Logger.LogWarning($"IWICImagingFactory.CreateBitmapFromHICON returned a null bitmap while converting a {FailureContext}.");
                return null;
            }

            return CreateSoftwareBitmapFromWicBitmap(wicBitmap, FailureContext);
        }
        finally
        {
            if (wicBitmap != null)
            {
                wicBitmap->Release();
            }

            if (imagingFactory != null)
            {
                imagingFactory->Release();
            }
        }
    }

    private static unsafe SoftwareBitmap? CreateSoftwareBitmapFromBitmapHandle(HBITMAP bitmapHandle)
    {
        const string FailureContext = "shell item bitmap";
        Wic.IWICBitmap* wicBitmap = null;

        if (!TryCreateImagingFactory(out var imagingFactory, FailureContext))
        {
            return null;
        }

        try
        {
            try
            {
                imagingFactory->CreateBitmapFromHBITMAP(
                    bitmapHandle,
                    default,
                    Wic.WICBitmapAlphaChannelOption.WICBitmapUsePremultipliedAlpha,
                    &wicBitmap);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"IWICImagingFactory.CreateBitmapFromHBITMAP failed while converting a {FailureContext}: {ex}");
                return null;
            }

            if (wicBitmap == null)
            {
                Logger.LogWarning($"IWICImagingFactory.CreateBitmapFromHBITMAP returned a null bitmap while converting a {FailureContext}.");
                return null;
            }

            return CreateSoftwareBitmapFromWicBitmap(wicBitmap, FailureContext);
        }
        finally
        {
            if (wicBitmap != null)
            {
                wicBitmap->Release();
            }

            if (imagingFactory != null)
            {
                imagingFactory->Release();
            }
        }
    }

    private static unsafe bool TryCreateImagingFactory(out Wic.IWICImagingFactory* imagingFactory, string failureContext)
    {
        imagingFactory = null;
        var hr = ShellNativeMethods.CoCreateInstance(ShellNativeMethods.CLSID_WICImagingFactory, out imagingFactory);
        if (hr.Failed || imagingFactory == null)
        {
            Logger.LogWarning($"CoCreateInstance(CLSID_WICImagingFactory) failed while converting a {failureContext}. HRESULT=0x{hr.Value:X8}.");
            return false;
        }

        return true;
    }

    private static unsafe SoftwareBitmap? CreateSoftwareBitmapFromWicBitmap(Wic.IWICBitmap* wicBitmap, string failureContext)
    {
        uint width = 0;
        uint height = 0;
        try
        {
            wicBitmap->GetSize(&width, &height);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"IWICBitmap.GetSize failed while converting a {failureContext}: {ex}");
            return null;
        }

        if (width == 0 || height == 0)
        {
            Logger.LogWarning($"IWICBitmap.GetSize returned an empty image while converting a {failureContext}.");
            return null;
        }

        var stride = checked(width * 4);
        var bufferSize = checked(height * stride);
        var pixels = new byte[bufferSize];

        try
        {
            fixed (byte* pixelBuffer = pixels)
            {
                wicBitmap->CopyPixels(null, stride, bufferSize, pixelBuffer);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"IWICBitmap.CopyPixels failed while converting a {failureContext}: {ex}");
            return null;
        }

        var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, checked((int)width), checked((int)height), BitmapAlphaMode.Premultiplied);
        bitmap.CopyFromBuffer(pixels.AsBuffer());
        return bitmap;
    }

    private static bool TryGetShellItemPath(string iconReference, out string shellPath)
    {
        shellPath = string.Empty;

        var candidatePath = PathHelper.Unquote(iconReference);
        if (Uri.TryCreate(candidatePath, UriKind.Absolute, out var absoluteUri) &&
            absoluteUri.IsFile &&
            !string.IsNullOrEmpty(absoluteUri.LocalPath))
        {
            shellPath = absoluteUri.LocalPath;
            return true;
        }

        if (!PathHelper.IsValidFilePath(candidatePath))
        {
            return false;
        }

        shellPath = Path.GetFullPath(candidatePath);
        return true;
    }

    private static string ResolveBinaryPath(string binaryPath)
    {
        var candidatePath = PathHelper.Unquote(binaryPath);

        if (Uri.TryCreate(candidatePath, UriKind.Absolute, out var absoluteUri) &&
            absoluteUri.IsFile &&
            !string.IsNullOrEmpty(absoluteUri.LocalPath))
        {
            return absoluteUri.LocalPath;
        }

        if (PathHelper.IsValidFilePath(candidatePath))
        {
            return Path.GetFullPath(candidatePath);
        }

        foreach (var searchRoot in EnumerateBinarySearchDirectories())
        {
            var combinedPath = Path.Combine(searchRoot, candidatePath);
            if (File.Exists(combinedPath))
            {
                return combinedPath;
            }
        }

        return candidatePath;
    }

    private static IEnumerable<string> EnumerateBinarySearchDirectories()
    {
        if (!string.IsNullOrEmpty(Environment.SystemDirectory))
        {
            yield return Environment.SystemDirectory;
        }

        var pathEntries = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEntries))
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in pathEntries.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrEmpty(entry) && seen.Add(entry))
            {
                yield return entry;
            }
        }
    }

    private static int ComposeIconSize(int iconSize)
    {
        var clampedSize = iconSize > 0 ? iconSize : DefaultIconSize;
        return (clampedSize << 16) | (clampedSize & 0xFFFF);
    }

    private static bool TryExtractIconBitmap(string path, int iconIndex, int size, bool preferExactRequestedSize, out SoftwareBitmap? bitmap)
    {
        bitmap = null;
        nint largeIcon = 0;
        nint smallIcon = 0;
        int hr;

        unsafe
        {
            if (preferExactRequestedSize)
            {
                hr = ShellNativeMethods.SHDefExtractIconW(
                    path,
                    iconIndex,
                    0,
                    &largeIcon,
                    null,
                    size);
            }
            else
            {
                hr = ShellNativeMethods.SHDefExtractIconW(
                    path,
                    iconIndex,
                    0,
                    &largeIcon,
                    &smallIcon,
                    ComposeIconSize(size));
            }
        }

        try
        {
            var iconHandle = largeIcon != 0 ? (HICON)largeIcon : (HICON)smallIcon;
            if (hr == 0 && !iconHandle.IsNull)
            {
                bitmap = CreateSoftwareBitmapFromIcon(iconHandle);
                return bitmap is not null;
            }
        }
        finally
        {
            if (largeIcon != 0)
            {
                PInvoke.DestroyIcon((HICON)largeIcon);
            }

            if (smallIcon != 0 && smallIcon != largeIcon)
            {
                PInvoke.DestroyIcon((HICON)smallIcon);
            }
        }

        return false;
    }
}
