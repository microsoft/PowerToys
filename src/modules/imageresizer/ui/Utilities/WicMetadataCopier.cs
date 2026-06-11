// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using ImageResizer.Models;
using Windows.Graphics.Imaging;

namespace ImageResizer.Utilities
{
    internal static class WicMetadataCopier
    {
        private static readonly Guid WicPixelFormat32bppBgra = new("6fddc324-4e03-4bfe-b185-3d77768dc90f");

        private static readonly string[] ExcludedMetadataPathFragments =
        [
            $"{{ushort={MetadataTagIds.Ifd.ImageWidth}}}",
            $"{{ushort={MetadataTagIds.Ifd.ImageHeight}}}",
            $"{{ushort={MetadataTagIds.Ifd.StripOffsets}}}",
            $"{{ushort={MetadataTagIds.Ifd.StripByteCounts}}}",
            $"{{ushort={MetadataTagIds.Ifd.TileOffsets}}}",
            $"{{ushort={MetadataTagIds.Ifd.TileByteCounts}}}",
            $"{{ushort={MetadataTagIds.Exif.PixelXDimension}}}",
            $"{{ushort={MetadataTagIds.Exif.PixelYDimension}}}",
            $"{{ushort={MetadataTagIds.Ifd.ThumbnailOffset}}}",
            $"{{ushort={MetadataTagIds.Ifd.ThumbnailLength}}}",
        ];

        public static string[] TryGetMetadataPropertyNames(string sourcePath, params string[] fallbackPropertyNames)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return fallbackPropertyNames ?? [];
            }

            object factory = null;
            object decoder = null;
            object frame = null;
            object reader = null;

            try
            {
                factory = new WicImagingFactory();
                decoder = ((IWicImagingFactory)factory).CreateDecoderFromFilename(sourcePath, IntPtr.Zero, WicAccessMode.Read, WicDecodeOptions.MetadataCacheOnLoad);
                frame = ((IWicBitmapDecoder)decoder).GetFrame(0);
                reader = ((IWicBitmapFrameDecode)frame).GetMetadataQueryReader();

                var names = new HashSet<string>(fallbackPropertyNames ?? [], StringComparer.OrdinalIgnoreCase);
                foreach (var metadataName in EnumerateMetadataNames((IWicMetadataQueryReader)reader))
                {
                    if (!ShouldSkipMetadataPath(metadataName))
                    {
                        names.Add(metadataName);
                    }
                }

                return [.. names];
            }
            catch
            {
                return fallbackPropertyNames ?? [];
            }
            finally
            {
                ReleaseComObject(reader);
                ReleaseComObject(frame);
                ReleaseComObject(decoder);
                ReleaseComObject(factory);
            }
        }

        public static async Task<bool> TryReencodeWithMetadataAsync(
            string sourcePath,
            BitmapDecoder decoder,
            Stream outputStream,
            Guid encoderGuid,
            float? jpegQuality,
            bool? pngInterlace,
            byte? tiffCompressionMethod,
            ResizeOperation.TransformPlan plan)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || decoder == null || outputStream == null)
            {
                return false;
            }

            object factory = null;
            object sourceDecoder = null;
            object encoder = null;
            var stream = new ComStreamWrapper(outputStream);

            try
            {
                var containerFormat = CodecHelper.GetContainerFormatGuidForEncoderId(encoderGuid);
                if (!containerFormat.HasValue)
                {
                    return false;
                }

                if (outputStream.CanSeek)
                {
                    outputStream.Position = 0;
                    outputStream.SetLength(0);
                }

                factory = new WicImagingFactory();
                sourceDecoder = ((IWicImagingFactory)factory).CreateDecoderFromFilename(sourcePath, IntPtr.Zero, WicAccessMode.Read, WicDecodeOptions.MetadataCacheOnLoad);

                var containerFormatGuid = containerFormat.Value;
                encoder = ((IWicImagingFactory)factory).CreateEncoder(ref containerFormatGuid, IntPtr.Zero);
                ((IWicBitmapEncoder)encoder).Initialize(stream, WicBitmapEncoderCacheOption.NoCache);

                var transform = new BitmapTransform();
                if (!plan.NoTransformNeeded)
                {
                    transform.ScaledWidth = plan.ScaledWidth;
                    transform.ScaledHeight = plan.ScaledHeight;
                    transform.InterpolationMode = BitmapInterpolationMode.Fant;

                    if (plan.CropBounds.HasValue)
                    {
                        transform.Bounds = plan.CropBounds.Value;
                    }
                }
                else
                {
                    transform.ScaledWidth = (uint)plan.OriginalWidth;
                    transform.ScaledHeight = (uint)plan.OriginalHeight;
                }

                uint outWidth = plan.CropBounds?.Width ?? (plan.NoTransformNeeded ? (uint)plan.OriginalWidth : plan.ScaledWidth);
                uint outHeight = plan.CropBounds?.Height ?? (plan.NoTransformNeeded ? (uint)plan.OriginalHeight : plan.ScaledHeight);

                for (uint i = 0; i < decoder.FrameCount; i++)
                {
                    IWicBitmapFrameEncode frameEncode = null;
                    IPropertyBag2 encoderOptions = null;
                    object sourceFrame = null;
                    object sourceMetadataReader = null;
                    IWicMetadataBlockReader metadataBlockReader = null;
                    IWicMetadataBlockWriter metadataBlockWriter = null;
                    IWicMetadataQueryWriter metadataWriter = null;

                    try
                    {
                        ((IWicBitmapEncoder)encoder).CreateNewFrame(out frameEncode, out encoderOptions);
                        ConfigureEncoderOptions(encoderOptions, encoderGuid, jpegQuality, pngInterlace, tiffCompressionMethod);
                        frameEncode.Initialize(encoderOptions);

                        sourceFrame = ((IWicBitmapDecoder)sourceDecoder).GetFrame(i);
                        sourceMetadataReader = ((IWicBitmapFrameDecode)sourceFrame).GetMetadataQueryReader();
                        metadataBlockReader = (IWicMetadataBlockReader)sourceFrame;
                        metadataBlockWriter = (IWicMetadataBlockWriter)frameEncode;
                        metadataBlockWriter.InitializeFromBlockReader(metadataBlockReader);

                        metadataWriter = frameEncode.GetMetadataQueryWriter();
                        RemoveInvalidMetadata((IWicMetadataQueryReader)sourceMetadataReader, metadataWriter);

                        var frame = await decoder.GetFrameAsync(i);
                        var pixelData = await frame.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);

                        var pixelFormat = WicPixelFormat32bppBgra;
                        var pixels = pixelData.DetachPixelData();
                        uint stride = checked(outWidth * 4);

                        frameEncode.SetSize(outWidth, outHeight);
                        frameEncode.SetResolution(frame.DpiX, frame.DpiY);
                        frameEncode.SetPixelFormat(ref pixelFormat);
                        frameEncode.WritePixels(outHeight, stride, (uint)pixels.Length, pixels);

                        frameEncode.Commit();
                    }
                    finally
                    {
                        ReleaseComObject(metadataBlockWriter);
                        ReleaseComObject(metadataBlockReader);
                        ReleaseComObject(metadataWriter);
                        ReleaseComObject(sourceMetadataReader);
                        ReleaseComObject(sourceFrame);
                        ReleaseComObject(encoderOptions);
                        ReleaseComObject(frameEncode);
                    }
                }

                ((IWicBitmapEncoder)encoder).Commit();
                outputStream.Flush();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WIC metadata re-encode failed for '{sourcePath}': {ex}");

                if (outputStream.CanSeek)
                {
                    outputStream.Position = 0;
                    outputStream.SetLength(0);
                }

                return false;
            }
            finally
            {
                ReleaseComObject(encoder);
                ReleaseComObject(sourceDecoder);
                ReleaseComObject(factory);
            }
        }

        internal static bool ShouldSkipMetadataPath(string metadataName)
        {
            foreach (var excludedFragment in ExcludedMetadataPathFragments)
            {
                if (metadataName.Contains(excludedFragment, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RemoveInvalidMetadata(IWicMetadataQueryReader reader, IWicMetadataQueryWriter writer)
        {
            if (reader == null || writer == null)
            {
                return;
            }

            foreach (var metadataName in EnumerateMetadataNames(reader))
            {
                if (!ShouldSkipMetadataPath(metadataName))
                {
                    continue;
                }

                try
                {
                    writer.RemoveMetadataByName(metadataName);
                }
                catch
                {
                }
            }
        }

        private static void ConfigureEncoderOptions(IPropertyBag2 encoderOptions, Guid encoderGuid, float? jpegQuality, bool? pngInterlace, byte? tiffCompressionMethod)
        {
            if (encoderOptions == null)
            {
                return;
            }

            var properties = new List<PROPBAG2>();
            var values = new List<object>();

            if (encoderGuid == BitmapEncoder.JpegEncoderId && jpegQuality.HasValue)
            {
                properties.Add(new PROPBAG2 { PstrName = "ImageQuality" });
                values.Add(jpegQuality.Value);
            }

            if (encoderGuid == BitmapEncoder.PngEncoderId && pngInterlace.HasValue)
            {
                properties.Add(new PROPBAG2 { PstrName = "InterlaceOption" });
                values.Add(pngInterlace.Value);
            }

            if (encoderGuid == BitmapEncoder.TiffEncoderId && tiffCompressionMethod.HasValue)
            {
                properties.Add(new PROPBAG2 { PstrName = "TiffCompressionMethod" });
                values.Add(tiffCompressionMethod.Value);
            }

            if (properties.Count > 0)
            {
                encoderOptions.Write(properties.Count, properties.ToArray(), values.ToArray());
            }
        }

        private static IEnumerable<string> EnumerateMetadataNames(IWicMetadataQueryReader reader)
        {
            IEnumString enumerator = null;
            try
            {
                enumerator = reader?.GetEnumerator();
                if (enumerator == null)
                {
                    yield break;
                }

                var location = GetLocation(reader);
                var buffer = new string[1];

                while (true)
                {
                    int hr = enumerator.Next(1, buffer, IntPtr.Zero);
                    if (hr != 0 || string.IsNullOrWhiteSpace(buffer[0]))
                    {
                        yield break;
                    }

                    yield return CombineMetadataPath(location, buffer[0]);
                }
            }
            finally
            {
                ReleaseComObject(enumerator);
            }
        }

        private static string GetLocation(IWicMetadataQueryReader reader)
        {
            try
            {
                reader.GetLocation(0, IntPtr.Zero, out uint actualLength);
                if (actualLength == 0)
                {
                    return string.Empty;
                }

                var locationBuffer = Marshal.AllocCoTaskMem(checked((int)actualLength * sizeof(char)));
                try
                {
                    reader.GetLocation(actualLength, locationBuffer, out _);
                    return Marshal.PtrToStringUni(locationBuffer) ?? string.Empty;
                }
                finally
                {
                    Marshal.FreeCoTaskMem(locationBuffer);
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string CombineMetadataPath(string location, string metadataName)
        {
            if (string.IsNullOrWhiteSpace(metadataName))
            {
                return string.Empty;
            }

            if (metadataName.StartsWith('/'))
            {
                return metadataName;
            }

            if (string.IsNullOrWhiteSpace(location))
            {
                return metadataName;
            }

            if (location.EndsWith('/'))
            {
                return location + metadataName;
            }

            return location + "/" + metadataName;
        }

        private static void ReleaseComObject(object instance)
        {
            if (instance != null && Marshal.IsComObject(instance))
            {
                Marshal.FinalReleaseComObject(instance);
            }
        }

        [ComImport]
        [Guid("cacaf262-9370-4615-a13b-9f5539da4c0a")]
        private class WicImagingFactory
        {
        }

        [ComImport]
        [Guid("ec5ec8a9-c395-4314-9c77-54d7a935ff70")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWicImagingFactory
        {
            IWicBitmapDecoder CreateDecoderFromFilename([MarshalAs(UnmanagedType.LPWStr)] string wzFilename, IntPtr pguidVendor, WicAccessMode desiredAccess, WicDecodeOptions metadataOptions);

            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Implements COM interface vtable layout")]
            void _VtblGap1_4();

            IWicBitmapEncoder CreateEncoder(ref Guid guidContainerFormat, IntPtr pguidVendor);
        }

        [ComImport]
        [Guid("9edde9e7-8dee-47ea-99df-e6faf2ed44bf")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWicBitmapDecoder
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Implements COM interface vtable layout")]
            void _VtblGap1_10();

            IWicBitmapFrameDecode GetFrame(uint index);
        }

        [ComImport]
        [Guid("3b16811b-6a43-4ec9-a813-3d930c13b940")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWicBitmapFrameDecode
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Implements COM interface vtable layout")]
            void _VtblGap1_5();

            IWicMetadataQueryReader GetMetadataQueryReader();
        }

        [ComImport]
        [Guid("30989668-e1c9-4597-b395-458eedb808df")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWicMetadataQueryReader
        {
            void GetContainerFormat(out Guid containerFormat);

            void GetLocation(uint cchMaxLength, IntPtr wzNamespace, out uint actualLength);

            void GetMetadataByName([MarshalAs(UnmanagedType.LPWStr)] string wzName, out IntPtr pvarValue);

            IEnumString GetEnumerator();
        }

        [ComImport]
        [Guid("00000103-a8f2-4877-ba0a-fd2b6645fb94")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWicBitmapEncoder
        {
            void Initialize(IStream pIStream, WicBitmapEncoderCacheOption cacheOption);

            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Implements COM interface vtable layout")]
            void _VtblGap1_6();

            void CreateNewFrame(out IWicBitmapFrameEncode ppIFrameEncode, out IPropertyBag2 ppIEncoderOptions);

            void Commit();
        }

        [ComImport]
        [Guid("00000105-a8f2-4877-ba0a-fd2b6645fb94")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWicBitmapFrameEncode
        {
            void Initialize(IPropertyBag2 pIEncoderOptions);

            void SetSize(uint uiWidth, uint uiHeight);

            void SetResolution(double dpiX, double dpiY);

            void SetPixelFormat(ref Guid pPixelFormat);

            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Implements COM interface vtable layout")]
            void _VtblGap4_3();

            void WritePixels(uint lineCount, uint cbStride, uint cbBufferSize, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] pbPixels);

            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Implements COM interface vtable layout")]
            void _VtblGap8_1();

            void Commit();

            IWicMetadataQueryWriter GetMetadataQueryWriter();
        }

        [ComImport]
        [Guid("a721791a-0def-4d06-bd91-2118bf1db10b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWicMetadataQueryWriter
        {
            void GetContainerFormat(out Guid containerFormat);

            void GetLocation(uint cchMaxLength, IntPtr wzNamespace, out uint actualLength);

            void GetMetadataByName([MarshalAs(UnmanagedType.LPWStr)] string wzName, out IntPtr pvarValue);

            IEnumString GetEnumerator();

            void SetMetadataByName([MarshalAs(UnmanagedType.LPWStr)] string wzName, IntPtr pvarValue);

            void RemoveMetadataByName([MarshalAs(UnmanagedType.LPWStr)] string wzName);
        }

        [ComImport]
        [Guid("feaa2a8d-b3f3-43e4-b25c-d1de990a1ae1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWicMetadataBlockReader
        {
            void GetContainerFormat(out Guid containerFormat);

            uint GetCount();

            [return: MarshalAs(UnmanagedType.IUnknown)]
            object GetReaderByIndex(uint index);

            IEnumUnknown GetEnumerator();
        }

        [ComImport]
        [Guid("08fb9676-b444-41e8-8dbe-6a53a542bff1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWicMetadataBlockWriter
        {
            void GetContainerFormat(out Guid containerFormat);

            uint GetCount();

            [return: MarshalAs(UnmanagedType.IUnknown)]
            object GetReaderByIndex(uint index);

            IEnumUnknown GetEnumerator();

            void InitializeFromBlockReader(IWicMetadataBlockReader reader);

            [return: MarshalAs(UnmanagedType.IUnknown)]
            object GetWriterByIndex(uint index);

            void AddWriter([MarshalAs(UnmanagedType.IUnknown)] object writer);

            void SetWriterByIndex(uint index, [MarshalAs(UnmanagedType.IUnknown)] object writer);

            void RemoveWriterByIndex(uint index);
        }

        [ComImport]
        [Guid("00000100-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IEnumUnknown
        {
            [PreserveSig]
            int Next(uint celt, [MarshalAs(UnmanagedType.IUnknown)] out object rgelt, out uint pceltFetched);

            [PreserveSig]
            int Skip(uint celt);

            void Reset();

            void Clone(out IEnumUnknown ppenum);
        }

        [ComImport]
        [Guid("22f55882-280b-11d0-a8a9-00a0c90c2004")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyBag2
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Implements COM interface vtable layout")]
            void _VtblGap1_1();

            void Write(int cProperties, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] PROPBAG2[] pPropBag, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] object[] pvarValue);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PROPBAG2
        {
            public uint DwType;
            public ushort Vt;
            public ushort CfType;
            public uint DwHint;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string PstrName;
            public Guid Clsid;
        }

        private enum WicDecodeOptions : int
        {
            MetadataCacheOnLoad = 1,
        }

        private enum WicBitmapEncoderCacheOption : int
        {
            InMemory = 0,
            TempFile = 1,
            NoCache = 2,
        }

        [Flags]
        private enum WicAccessMode : int
        {
            Read = unchecked((int)0x80000000),
        }

        private sealed class ComStreamWrapper : IStream
        {
            private readonly Stream _stream;

            public ComStreamWrapper(Stream stream)
            {
                _stream = stream;
            }

            public void Read(byte[] pv, int cb, IntPtr pcbRead)
            {
                int read = _stream.Read(pv, 0, cb);
                if (pcbRead != IntPtr.Zero)
                {
                    Marshal.WriteInt32(pcbRead, read);
                }
            }

            public void Write(byte[] pv, int cb, IntPtr pcbWritten)
            {
                _stream.Write(pv, 0, cb);
                if (pcbWritten != IntPtr.Zero)
                {
                    Marshal.WriteInt32(pcbWritten, cb);
                }
            }

            public void Seek(long dlibMove, int dwOrigin, IntPtr plibNewPosition)
            {
                long position = _stream.Seek(dlibMove, (SeekOrigin)dwOrigin);
                if (plibNewPosition != IntPtr.Zero)
                {
                    Marshal.WriteInt64(plibNewPosition, position);
                }
            }

            public void SetSize(long libNewSize)
            {
                _stream.SetLength(libNewSize);
            }

            public void CopyTo(IStream pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten)
            {
                throw new NotSupportedException();
            }

            public void Commit(int grfCommitFlags)
            {
                _stream.Flush();
            }

            public void Revert()
            {
                throw new NotSupportedException();
            }

            public void LockRegion(long libOffset, long cb, int dwLockType)
            {
                throw new NotSupportedException();
            }

            public void UnlockRegion(long libOffset, long cb, int dwLockType)
            {
                throw new NotSupportedException();
            }

            public void Stat(out STATSTG pstatstg, int grfStatFlag)
            {
                pstatstg = new STATSTG
                {
                    cbSize = _stream.CanSeek ? _stream.Length : 0,
                    type = 2,
                };
            }

            public void Clone(out IStream ppstm)
            {
                throw new NotSupportedException();
            }
        }
    }
}
