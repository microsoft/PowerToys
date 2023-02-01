// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using DrawingImaging = System.Drawing.Imaging;
using MediaImaging = System.Windows.Media.Imaging;

namespace PowerLauncher.Helper
{
    // based on: https://stackoverflow.com/questions/61041282/showing-image-thumbnail-with-mouse-cursor-while-dragging/61148788#61148788
    public static class DragDataObject
    {
        private static readonly Guid DataObject = new Guid("b8c0bd9f-ed24-455c-83e6-d5390c4fe8c4");

        public static IDataObject FromFile(string filePath)
        {
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(filePath, null, typeof(IShellItem).GUID, out IShellItem item));
            Marshal.ThrowExceptionForHR(item.BindToHandler(null, DataObject, typeof(IDataObject).GUID, out object dataObject));
            return (IDataObject)dataObject;
        }

        public static void SetDragImage(this IDataObject dataObject, IntPtr hBitmap, int width, int height)
        {
            if (dataObject == null)
            {
                throw new ArgumentNullException(nameof(dataObject));
            }

            IDragSourceHelper dragDropHelper = (IDragSourceHelper)new DragDropHelper();
            ShDragImage dragImage = new ShDragImage
            {
                HBmpDragImage = hBitmap,
                SizeDragImage = new Size(width, height),
            };
            Marshal.ThrowExceptionForHR(dragDropHelper.InitializeFromBitmap(ref dragImage, dataObject));
        }

        [DllImport("shell32", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(string path, IBindCtx pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem ppv);

        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            [PreserveSig]
            int BindToHandler(IBindCtx pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

            // more methods available, but we don't need them
        }

        [ComImport]
        [Guid("4657278a-411b-11d2-839a-00c04fd918d0")] // CLSID_DragDropHelper
        private class DragDropHelper
        {
        }

        // https://docs.microsoft.com/en-us/windows/win32/api/shobjidl_core/ns-shobjidl_core-shdragimage
        [StructLayout(LayoutKind.Sequential)]
        private struct ShDragImage
        {
            public Size SizeDragImage;
            public Point PtOffset;
            public IntPtr HBmpDragImage;
            public int CrColorKey;
        }

        // https://docs.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-idragsourcehelper
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("DE5BF786-477A-11D2-839D-00C04FD918D0")]
        private interface IDragSourceHelper
        {
            [PreserveSig]
            int InitializeFromBitmap(ref ShDragImage pShDrawImage, IDataObject pDataObject);

            // more methods available, but we don't need them
        }

        // https://stackoverflow.com/a/2897325
        public static Bitmap BitmapSourceToBitmap(MediaImaging.BitmapSource source)
        {
            if (source == null)
            {
                return null;
            }

            Bitmap bitmap = new Bitmap(source.PixelWidth, source.PixelHeight, DrawingImaging.PixelFormat.Format32bppArgb);
            DrawingImaging.BitmapData bitmapData = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), DrawingImaging.ImageLockMode.WriteOnly, DrawingImaging.PixelFormat.Format32bppArgb);

            source.CopyPixels(System.Windows.Int32Rect.Empty, bitmapData.Scan0, bitmapData.Height * bitmapData.Stride, bitmapData.Stride);
            bitmap.UnlockBits(bitmapData);

            return bitmap;
        }
    }
}
