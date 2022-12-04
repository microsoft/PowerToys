// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml.Media.Imaging;
    using WIC;
    using File = Peek.Common.Models.File;

    [INotifyPropertyChanged]
    public partial class ImagePreviewer : IDisposable
    {
        // Based on https://stackoverflow.com/questions/21751747/extract-thumbnail-for-any-file-in-windows
        private const string IShellItem2Guid = "7E9FB0D3-919F-4307-AB2E-9B1860310C93";

        [ObservableProperty]
        private BitmapSource? preview;

        public ImagePreviewer(File file)
        {
            File = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        private File File { get; }

        private DispatcherQueue Dispatcher { get; }

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public Task<Windows.Foundation.Size> GetPreviewSizeAsync()
        {
            IWICImagingFactory factory = (IWICImagingFactory)new WICImagingFactoryClass();
            var decoder = factory.CreateDecoderFromFilename(File.Path, IntPtr.Zero, StreamAccessMode.GENERIC_READ, WICDecodeOptions.WICDecodeMetadataCacheOnLoad);
            var frame = decoder?.GetFrame(0);
            int width = 0;
            int height = 0;
            frame?.GetSize(out width, out height);

            return Task.FromResult(new Windows.Foundation.Size(width, height));
        }

        public Task LoadPreviewAsync()
        {
            bool isFullImageLoaded = false;

            var thumbnailTCS = new TaskCompletionSource();
            Dispatcher.TryEnqueue(() =>
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                if (!isFullImageLoaded)
                {
                    var thumbnail = GetThumbnail(File.Path);
                    Preview = thumbnail;
                }

                thumbnailTCS.SetResult();
            });

            var fullImageTCS = new TaskCompletionSource();
            Dispatcher.TryEnqueue(async () =>
            {
                // TODO: Check if this is performant
                var bitmap = await LoadFullImageAsync(File.Path);
                isFullImageLoaded = true;

                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                Preview = bitmap;
                fullImageTCS.SetResult();
            });

            return Task.WhenAll(thumbnailTCS.Task, fullImageTCS.Task);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }

        private static async Task<BitmapImage> LoadFullImageAsync(string path)
        {
            var bitmap = new BitmapImage();
            using (FileStream stream = System.IO.File.OpenRead(path))
            {
                await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
            }

            return bitmap;
        }

        private static BitmapSource GetThumbnail(string fileName)
        {
            IntPtr hbitmap;
            HResult hr = GetThumbnailImpl(Path.GetFullPath(fileName), out hbitmap);

            try
            {
                var bitmap = System.Drawing.Image.FromHbitmap(hbitmap);
                var bitmapImage = new BitmapImage();
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    stream.Position = 0;
                    bitmapImage.SetSource(stream.AsRandomAccessStream());
                }

                return bitmapImage;
            }
            finally
            {
                // delete HBitmap to avoid memory leaks
                DeleteObject(hbitmap);
            }
        }

        private static HResult GetThumbnailImpl(string filename, out IntPtr hbitmap)
        {
            Guid shellItem2Guid = new Guid(IShellItem2Guid);
            int retCode = SHCreateItemFromParsingName(filename, IntPtr.Zero, ref shellItem2Guid, out IShellItem nativeShellItem);

            if (retCode != 0)
            {
                throw Marshal.GetExceptionForHR(retCode)!;
            }

            var extraLarge = new NativeSize { Width = 720, Height = 720, };
            var large = new NativeSize { Width = 256, Height = 256 };
            var medium = new NativeSize { Width = 96, Height = 96 };
            var small = new NativeSize { Width = 32, Height = 32 };

            var options = ThumbnailOptions.BiggerSizeOk | ThumbnailOptions.ThumbnailOnly | ThumbnailOptions.ScaleUp;

            HResult hr = ((IShellItemImageFactory)nativeShellItem).GetImage(large, options, out hbitmap);

            if (hr != HResult.Ok)
            {
                hr = ((IShellItemImageFactory)nativeShellItem).GetImage(large, options, out hbitmap);
            }

            if (hr != HResult.Ok)
            {
                hr = ((IShellItemImageFactory)nativeShellItem).GetImage(medium, options, out hbitmap);
            }

            if (hr != HResult.Ok)
            {
                hr = ((IShellItemImageFactory)nativeShellItem).GetImage(small, options, out hbitmap);
            }

            Marshal.ReleaseComObject(nativeShellItem);

            return hr;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr hObject);

        [Flags]
        public enum ThumbnailOptions
        {
            None = 0x00,
            BiggerSizeOk = 0x01,
            InMemoryOnly = 0x02,
            IconOnly = 0x04,
            ThumbnailOnly = 0x08,
            InCacheOnly = 0x10,
            ScaleUp = 0x100,
        }

        public enum HResult
        {
            Ok = 0x0000,
            False = 0x0001,
            InvalidArguments = unchecked((int)0x80070057),
            OutOfMemory = unchecked((int)0x8007000E),
            NoInterface = unchecked((int)0x80004002),
            Fail = unchecked((int)0x80004005),
            ExtractionFailed = unchecked((int)0x8004B200),
            ElementNotFound = unchecked((int)0x80070490),
            TypeElementNotFound = unchecked((int)0x8002802B),
            NoObject = unchecked((int)0x800401E5),
            Win32ErrorCanceled = 1223,
            Canceled = unchecked((int)0x800704C7),
            ResourceInUse = unchecked((int)0x800700AA),
            AccessDenied = unchecked((int)0x80030005),
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellItemImageFactory
        {
            [PreserveSig]
            HResult GetImage(
            [In, MarshalAs(UnmanagedType.Struct)] NativeSize size,
            [In] ThumbnailOptions flags,
            [Out] out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NativeSize
        {
            private int width;
            private int height;

            public int Width
            {
                set { width = value; }
            }

            public int Height
            {
                set { height = value; }
            }
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        internal interface IShellItem
        {
            void BindToHandler(
                IntPtr pbc,
                [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
                [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
                out IntPtr ppv);

            void GetParent(out IShellItem ppsi);

            void GetDisplayName(Sigdn sigdnName, out IntPtr ppszName);

            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        public enum Sigdn : uint
        {
            NormalDisplay = 0,
            ParentRelativeParsing = 0x80018001,
            ParentRelativeForAddressBar = 0x8001c001,
            DesktopAbsoluteParsing = 0x80028000,
            ParentRelativeEditing = 0x80031001,
            DesktopAbsoluteEditing = 0x8004c000,
            FileSysPath = 0x80058000,
            Url = 0x80068000,
        }
    }
}
