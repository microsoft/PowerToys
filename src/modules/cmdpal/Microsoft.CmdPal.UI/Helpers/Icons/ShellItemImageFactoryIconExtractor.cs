// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Graphics.Imaging;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;

namespace Microsoft.CmdPal.UI.Helpers;

internal static partial class ShellItemImageFactoryIconExtractor
{
    public static unsafe SoftwareBitmap? Extract(string path, int size)
    {
        if (size <= 0)
        {
            return null;
        }

        using var errorMode = ShellThreadErrorModeScope.SuppressShellDialogs();
        IShellItemImageFactory? factory = null;
        nint bitmapHandle = 0;
        try
        {
            var interfaceId = typeof(IShellItemImageFactory).GUID;
            if (NativeMethods.SHCreateItemFromParsingName(path, 0, in interfaceId, out factory) < 0 || factory is null)
            {
                return null;
            }

            // Preserve Shell's native padding for small icons instead of stretching a jumbo image-list entry.
            if (factory.GetImage(
                new ImageSize { Width = size, Height = size },
                (uint)(SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_BIGGERSIZEOK | SIIGBF.SIIGBF_CROPTOSQUARE),
                out bitmapHandle) < 0 || bitmapHandle == 0)
            {
                return null;
            }

            var hBitmap = new HBITMAP(bitmapHandle);
            var source = default(BITMAP);
            if (PInvoke.GetObject(hBitmap, sizeof(BITMAP), &source) == 0 || source.bmWidth <= 0 || source.bmHeight <= 0)
            {
                return null;
            }

            // BIGGERSIZEOK can return more pixels than requested. Read the actual bitmap dimensions.
            var pixels = new byte[checked(source.bmWidth * source.bmHeight * 4)];
            var info = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)sizeof(BITMAPINFOHEADER),
                    biWidth = source.bmWidth,
                    biHeight = -source.bmHeight,
                    biPlanes = 1,
                    biBitCount = 32,
                },
            };

            var dc = PInvoke.GetDC(default);
            try
            {
                fixed (byte* buffer = pixels)
                {
                    if (PInvoke.GetDIBits(dc, hBitmap, 0, (uint)source.bmHeight, buffer, &info, DIB_USAGE.DIB_RGB_COLORS) != source.bmHeight)
                    {
                        return null;
                    }
                }
            }
            finally
            {
                _ = PInvoke.ReleaseDC(default, dc);
            }

            // Shell's bitmap contains straight alpha, including its translucent frame. WinUI requires premultiplied pixels.
            using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(), BitmapPixelFormat.Bgra8, source.bmWidth, source.bmHeight, BitmapAlphaMode.Straight);
            return SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (bitmapHandle != 0)
            {
                _ = PInvoke.DeleteObject(new HBITMAP(bitmapHandle));
            }

            if (factory != null)
            {
                ((ComObject)(object)factory).FinalRelease();
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ImageSize
    {
        public int Width;
        public int Height;
    }

    [GeneratedComInterface]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal partial interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(ImageSize size, uint flags, out nint bitmap);
    }

    private static partial class NativeMethods
    {
        [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial int SHCreateItemFromParsingName(
            string path,
            nint bindContext,
            in Guid interfaceId,
            [MarshalUsing(typeof(UniqueComInterfaceMarshaller<IShellItemImageFactory>))] out IShellItemImageFactory? factory);
    }
}
