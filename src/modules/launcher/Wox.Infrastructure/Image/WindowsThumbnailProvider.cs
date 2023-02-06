// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Wox.Plugin.Logger;

namespace Wox.Infrastructure.Image
{
    [Flags]
    public enum ThumbnailOptions
    {
        RESIZETOFIT = 0x00,
        BiggerSizeOk = 0x01,
        InMemoryOnly = 0x02,
        IconOnly = 0x04,
        ThumbnailOnly = 0x08,
        InCacheOnly = 0x10,
    }

    public static class WindowsThumbnailProvider
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;

        // Based on https://stackoverflow.com/questions/21751747/extract-thumbnail-for-any-file-in-windows
        private const string IShellItem2Guid = "7E9FB0D3-919F-4307-AB2E-9B1860310C93";

        internal enum HResult
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

        public static BitmapSource GetThumbnail(string fileName, int width, int height, ThumbnailOptions options)
        {
            IntPtr hBitmap = GetHBitmap(Path.GetFullPath(fileName), width, height, options);

            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                // delete HBitmap to avoid memory leaks
                NativeMethods.DeleteObject(hBitmap);
            }
        }

        private static IntPtr GetHBitmap(string fileName, int width, int height, ThumbnailOptions options)
        {
            Guid shellItem2Guid = new Guid(IShellItem2Guid);
            int retCode = NativeMethods.SHCreateItemFromParsingName(fileName, IntPtr.Zero, ref shellItem2Guid, out IShellItem nativeShellItem);

            if (retCode != 0)
            {
                throw Marshal.GetExceptionForHR(retCode);
            }

            NativeSize nativeSize = new NativeSize
            {
                Width = width,
                Height = height,
            };

            HResult hr = ((IShellItemImageFactory)nativeShellItem).GetImage(nativeSize, options, out IntPtr hBitmap);

            // if extracting image thumbnail and failed, extract shell icon
            if (options == ThumbnailOptions.ThumbnailOnly && hr == HResult.ExtractionFailed)
            {
                hr = ((IShellItemImageFactory)nativeShellItem).GetImage(nativeSize, ThumbnailOptions.IconOnly, out hBitmap);
            }

            Marshal.ReleaseComObject(nativeShellItem);

            if (hr == HResult.Ok)
            {
                return hBitmap;
            }

            throw new InvalidComObjectException($"Error while extracting thumbnail for {fileName}", Marshal.GetExceptionForHR((int)hr));
        }

        private static bool logReportedAdobeReaderDetected; // Keep track if Adobe Reader detection has been logged yet.
        private static bool logReportedErrorInDetectingAdobeReader; // Keep track if we reported an exception while trying to detect Adobe Reader yet.
        private static bool adobeReaderDetectionLastResult; // The last result when Adobe Reader detection has read the registry.
        private static DateTime adobeReaderDetectionLastTime; // The last time when Adobe Reader detection has read the registry.

        // We have to evaluate this in real time to not crash, if the user switches to Adobe Reader/Acrobat Pro after starting PT Run.
        // Adobe registers its thumbnail handler always in "HKCR\Acrobat.Document.*\shellex\{BB2E617C-0920-11d1-9A0B-00C04FC2D6C1}".
        public static bool DoesPdfUseAcrobatAsProvider()
        {
            // If the last run is not more than five seconds ago use its result.
            // Doing this we minimize the amount of Registry queries, if more than one new PDF file is shown in the results.
            if ((DateTime.Now - adobeReaderDetectionLastTime).TotalSeconds < 5)
            {
                return adobeReaderDetectionLastResult;
            }

            // If cache condition is false, then query the registry.
            try
            {
                // First detect if there is a provider other than Adobe. For example PowerToys.
                // Generic GUIDs used by Explorer to identify the configured provider types: {E357FCCD-A995-4576-B01F-234630154E96} = File thumbnail, {BB2E617C-0920-11d1-9A0B-00C04FC2D6C1} = Image thumbnail.
                RegistryKey key1 = Registry.ClassesRoot.OpenSubKey(".pdf\\shellex\\{E357FCCD-A995-4576-B01F-234630154E96}", false);
                string value1 = (string)key1?.GetValue(string.Empty);
                key1?.Close();
                RegistryKey key2 = Registry.ClassesRoot.OpenSubKey(".pdf\\shellex\\{BB2E617C-0920-11d1-9A0B-00C04FC2D6C1}", false);
                string value2 = (string)key2?.GetValue(string.Empty);
                key2?.Close();
                if (!string.IsNullOrEmpty(value1) || !string.IsNullOrEmpty(value2))
                {
                    // A provider other than Adobe is used. (For example the PowerToys PDF Thumbnail provider.)
                    logReportedAdobeReaderDetected = false; // Reset log marker to report when Adobe is reused. (Example: Adobe -> Test PowerToys. -> Back to Adobe.)
                    adobeReaderDetectionLastResult = false;
                    adobeReaderDetectionLastTime = DateTime.Now;
                    return false;
                }

                // Then detect if Adobe is the default PDF application.
                // The global config can be found under "HKCR\.pdf", but the "UserChoice" key under HKCU has precedence.
                RegistryKey pdfKeyUser = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\.pdf\\UserChoice", false);
                string pdfAppUser = (string)pdfKeyUser?.GetValue("ProgId");
                pdfKeyUser?.Close();
                RegistryKey pdfKeyGlobal = Registry.ClassesRoot.OpenSubKey(".pdf", false);
                string pdfAppGlobal = (string)pdfKeyGlobal?.GetValue(string.Empty);
                pdfKeyGlobal?.Close();
                string pdfApp = !string.IsNullOrEmpty(pdfAppUser) ? pdfAppUser : pdfAppGlobal; // User choice has precedence.
                if (string.IsNullOrEmpty(pdfApp) || !pdfApp.StartsWith("Acrobat.Document.", StringComparison.OrdinalIgnoreCase))
                {
                    // Adobe is not used as PDF application.
                    logReportedAdobeReaderDetected = false; // Reset log marker to report when Adobe is reused. (Example: Adobe -> Test PowerToys. -> Back to Adobe.)
                    adobeReaderDetectionLastResult = false;
                    adobeReaderDetectionLastTime = DateTime.Now;
                    return false;
                }

                // Detect if the thumbnail handler from Adobe is disabled.
                RegistryKey adobeAppKey = Registry.ClassesRoot.OpenSubKey(pdfApp + "\\shellex\\{BB2E617C-0920-11d1-9A0B-00C04FC2D6C1}", false);
                string adobeAppProvider = (string)adobeAppKey?.GetValue(string.Empty);
                adobeAppKey?.Close();
                if (string.IsNullOrEmpty(adobeAppProvider))
                {
                    // No Adobe handler.
                    logReportedAdobeReaderDetected = false; // Reset log marker to report when Adobe is reused. (Example: Adobe -> Test PowerToys. -> Back to Adobe.)
                    adobeReaderDetectionLastResult = false;
                    adobeReaderDetectionLastTime = DateTime.Now;
                    return false;
                }

                // Thumbnail handler from Adobe is enabled and used.
                if (!logReportedAdobeReaderDetected)
                {
                    logReportedAdobeReaderDetected = true;
                    Log.Info("Adobe Reader / Adobe Acrobat Pro has been detected as the PDF thumbnail provider.", MethodBase.GetCurrentMethod().DeclaringType);
                }

                adobeReaderDetectionLastResult = true;
                adobeReaderDetectionLastTime = DateTime.Now;
                return true;
            }
            catch (System.Exception ex)
            {
                if (!logReportedErrorInDetectingAdobeReader)
                {
                    logReportedErrorInDetectingAdobeReader = true;
                    Log.Exception("Got an exception while trying to detect Adobe Reader / Adobe Acrobat Pro as PDF thumbnail provider. To prevent PT Run from a Dispatcher crash, we report that Adobe Reader / Adobe Acrobat Pro is used and show only the PDF icon in the results.", ex, MethodBase.GetCurrentMethod().DeclaringType);
                }

                // If we fail to detect it, we return that Adobe is used. Otherwise we could run into the Dispatcher crash.
                // (This only results in showing the icon instead of a thumbnail. It has no other functional impact.)
                return true;
            }
        }
    }
}
