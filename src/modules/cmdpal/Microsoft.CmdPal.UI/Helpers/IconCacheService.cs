// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.Terminal.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.DirectWrite;
using Windows.Win32.Graphics.Gdi;

namespace Microsoft.CmdPal.UI.Helpers;

public sealed class IconCacheService
{
    private readonly DispatcherQueue dispatcherQueue;
    private IDWriteFontFace? fontFace;
    private IDWriteRenderingParams? renderingParams;
    private IDWriteGdiInterop? interop;

    public IconCacheService(DispatcherQueue dispatcherQueue)
    {
        this.dispatcherQueue = dispatcherQueue;
        this.InitDwrite();
    }

    public Task<IconSource?> GetIconSource(IconDataViewModel icon) =>

        // todo: actually implement a cache of some sort
        IconToSource(icon);

    private async Task<IconSource?> IconToSource(IconDataViewModel icon)
    {
        try
        {
            if (!string.IsNullOrEmpty(icon.Icon))
            {
                if (FontIconGlyphClassifier.Classify(icon.Icon) == FontIconGlyphKind.Emoji)
                {
                    // use leonard's magic
                    if (ImageSourceToIcon(MagicEmoji(icon.Icon)) is IconSource ico)
                    {
                        return ico;
                    }
                }

                var source = IconPathConverter.IconSourceMUX(icon.Icon, false, icon.FontFamily);
                return source;
            }
            else if (icon.Data is not null)
            {
                try
                {
                    return await StreamToIconSource(icon.Data.Unsafe!);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Failed to load icon from stream: " + ex);
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private async Task<IconSource?> StreamToIconSource(IRandomAccessStreamReference iconStreamRef)
    {
        if (iconStreamRef is null)
        {
            return null;
        }

        var bitmap = await IconStreamToBitmapImageAsync(iconStreamRef);
        var icon = new ImageIconSource() { ImageSource = bitmap };
        return icon;
    }

    private async Task<BitmapImage> IconStreamToBitmapImageAsync(IRandomAccessStreamReference iconStreamRef)
    {
        // Return the bitmap image via TaskCompletionSource. Using WCT's EnqueueAsync does not suffice here, since if
        // we're already on the thread of the DispatcherQueue then it just directly calls the function, with no async involved.
        return await TryEnqueueAsync(dispatcherQueue, async () =>
        {
            using var bitmapStream = await iconStreamRef.OpenReadAsync();
            var itemImage = new BitmapImage();
            await itemImage.SetSourceAsync(bitmapStream);
            return itemImage;
        });
    }

    private static Task<T> TryEnqueueAsync<T>(DispatcherQueue dispatcher, Func<Task<T>> function)
    {
        var completionSource = new TaskCompletionSource<T>();

        var enqueued = dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, async void () =>
        {
            try
            {
                var result = await function();
                completionSource.SetResult(result);
            }
            catch (Exception ex)
            {
                completionSource.SetException(ex);
            }
        });

        if (!enqueued)
        {
            completionSource.SetException(new InvalidOperationException("Failed to enqueue the operation on the UI dispatcher"));
        }

        return completionSource.Task;
    }

    /// <summary>
    /// Initializes DirectWrite and related objects needed for rendering emoji.
    /// </summary>
    private void InitDwrite()
    {
        unsafe
        {
            var factory = Native.CreateDWriteCoreFactory();
            factory.CreateRenderingParams(out renderingParams);
            factory.GetGdiInterop(out interop);

            interop.CreateBitmapRenderTarget(HDC.Null, 100, 100, out var renderTarget);
            var renderTarget3 = (IDWriteBitmapRenderTarget3)renderTarget;

            // Get the font face
            {
                factory.GetSystemFontCollection(out var fontCollection, false);
                fontCollection.FindFamilyName("Segoe UI Emoji", out var index, out var exists);
                fontCollection.GetFontFamily(index, out var fontFamily);
                fontFamily.GetFirstMatchingFont(
                    DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL,
                    DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL,
                    DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL,
                    out var font);
                font.CreateFontFace(out fontFace);
            }
        }
    }

    /// <summary>
    /// Renders an emoji glyph to an ImageSource. Unbelievably, it is faster to
    /// MANUALLY render the emoji using DirectWrite than it is to ask the normal
    /// WinUI font icon renderer to render it.
    ///
    /// Big shoutout to @lhecker for writing this for us
    /// </summary>
    /// <param name="glyph">The emoji glyph to render.</param>
    /// <returns>An ImageSource containing the rendered emoji, or null if
    /// rendering failed.</returns>
    private ImageSource? MagicEmoji(string glyph)
    {
        if (string.IsNullOrEmpty(glyph))
        {
            return null;
        }

        if (fontFace is null)
        {
            return null;
        }

        if (interop is null)
        {
            return null;
        }

        var size = 54;
        unsafe
        {
            interop.CreateBitmapRenderTarget(HDC.Null, (uint)size, (uint)size, out var renderTarget);
            var renderTarget3 = (IDWriteBitmapRenderTarget3)renderTarget;

            var glyphIndices = new ushort[1];
            List<uint> codepoints = [];
            for (var i = 0; i < glyph.Length; i += char.IsSurrogatePair(glyph, i) ? 2 : 1)
            {
                var x = char.ConvertToUtf32(glyph, i);
                codepoints.Add((uint)x);
            }

            fontFace.GetGlyphIndices(codepoints.ToArray(), 1, glyphIndices);

            var glyphIndex = glyphIndices[0];
            var advance = (float)0.0f;
            var offset = new DWRITE_GLYPH_OFFSET { };
            var run = new DWRITE_GLYPH_RUN
            {
                fontFace = fontFace,
                fontEmSize = 48,
                glyphCount = 1,
                glyphIndices = &glyphIndex,
                glyphAdvances = &advance,
                glyphOffsets = &offset,
                isSideways = false,
                bidiLevel = 0,
            };
            var rect = new RECT { };
            renderTarget3.DrawGlyphRunWithColorSupport(
                -6,
                (float)45.0f,
                DWRITE_MEASURING_MODE.DWRITE_MEASURING_MODE_NATURAL,
                in run,
                renderingParams,
                new COLORREF(0xffffffff),
                0,
                &rect);

            renderTarget3.GetBitmapData(out var bitmapData);

            var bitmap = new WriteableBitmap(size, size);
            using (var stream = bitmap.PixelBuffer.AsStream())
            {
                var pixels = new Span<uint>(bitmapData.pixels, size * size);
                var bytes = MemoryMarshal.AsBytes(pixels);
                stream.Write(bytes.ToArray(), 0, bytes.Length);
            }

            return bitmap;
        }
    }

    private static IconSource? ImageSourceToIcon(ImageSource? img)
    {
        return img is null ? null : new ImageIconSource() { ImageSource = img };
    }

    internal sealed partial class Native
    {
        [DllImport("DWriteCore.dll", ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        public static extern HRESULT DWriteCoreCreateFactory(DWRITE_FACTORY_TYPE factoryType, in Guid iid, out IntPtr factory);

        public static unsafe IDWriteFactory8 CreateDWriteCoreFactory()
        {
            var iid = typeof(IDWriteFactory8).GUID;
            var hr = DWriteCoreCreateFactory(DWRITE_FACTORY_TYPE.DWRITE_FACTORY_TYPE_SHARED, in iid, out var factory);
#pragma warning disable CA2201 // Do not raise reserved exception types
            return hr.Failed
                ? throw new COMException("DWriteCoreCreateFactory failed", hr)
                : (IDWriteFactory8)Marshal.GetObjectForIUnknown(factory);
#pragma warning restore CA2201 // Do not raise reserved exception types
        }
    }
}
