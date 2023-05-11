using System;
using System.ComponentModel;

namespace WIC
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class IWICImagingFactoryExtensions
    {
        public static IWICBitmap CreateBitmap(this IWICImagingFactory imagingFactory, Size size, Guid pixelFormat, WICBitmapCreateCacheOption option)
        {
            return imagingFactory.CreateBitmap(size.Width, size.Height, pixelFormat, option);
        }

        public static IWICBitmap CreateBitmapFromSourceRect(this IWICImagingFactory imagingFactory, IWICBitmapSource pIBitmapSource, WICRect rect)
        {
            return imagingFactory.CreateBitmapFromSourceRect(pIBitmapSource, rect.X, rect.Y, rect.Width, rect.Height);
        }

        public static IWICBitmap CreateBitmapFromMemory(this IWICImagingFactory imagingFactory, Size size, Guid pixelFormat, int cbStride, byte[] pbBuffer)
        {
            return imagingFactory.CreateBitmapFromMemory(size.Width, size.Height, pixelFormat, cbStride, pbBuffer.Length, pbBuffer);
        }

        public static IWICBitmapDecoder CreateDecoder(this IWICImagingFactory imagingFactory, Guid guidContainerFormat, Guid? pguidVendor = null)
        {
            using (var pguidVendorPtr = CoTaskMemPtr.From(pguidVendor))
            {
                return imagingFactory.CreateDecoder(guidContainerFormat, pguidVendorPtr);
            }
        }

        public static IWICBitmapDecoder CreateDecoderFromFileHandle(this IWICImagingFactory imagingFactory, IntPtr hFile, WICDecodeOptions metadataOptions, Guid? pguidVendor = null)
        {
            using (var pguidVendorPtr = CoTaskMemPtr.From(pguidVendor))
            {
                return imagingFactory.CreateDecoderFromFileHandle(hFile, pguidVendorPtr, metadataOptions);
            }
        }

        public static IWICBitmapDecoder CreateDecoderFromFilename(this IWICImagingFactory imagingFactory, string wzFilename, Guid? pguidVendor = null, WICDecodeOptions metadataOptions = WICDecodeOptions.WICDecodeMetadataCacheOnDemand)
        {
            using (var pguidVendorPtr = CoTaskMemPtr.From(pguidVendor))
            {
                return imagingFactory.CreateDecoderFromFilename(wzFilename, pguidVendorPtr, StreamAccessMode.GENERIC_READ, metadataOptions);
            }
        }

        public static IWICBitmapDecoder CreateDecoderFromStream(this IWICImagingFactory imagingFactory, IStream pIStream, WICDecodeOptions metadataOptions, Guid? pguidVendor = null)
        {
            using (var pguidVendorPtr = CoTaskMemPtr.From(pguidVendor))
            {
                return imagingFactory.CreateDecoderFromStream(pIStream, pguidVendorPtr, metadataOptions);
            }
        }

        public static IWICBitmapEncoder CreateEncoder(this IWICImagingFactory factory, Guid guidContainerFormat, Guid? pguidVendor = null)
        {
            using (var pguidVendorPtr = CoTaskMemPtr.From(pguidVendor))
            {
                return factory.CreateEncoder(guidContainerFormat, pguidVendorPtr);
            }
        }

        public static IWICMetadataQueryWriter CreateQueryWriter(this IWICImagingFactory imagingFactory, Guid guidMetadataFormat, Guid? pguidVendor = null)
        {
            using (var pguidVendorPtr = CoTaskMemPtr.From(pguidVendor))
            {
                return imagingFactory.CreateQueryWriter(guidMetadataFormat, pguidVendorPtr);
            }
        }

        public static IWICMetadataQueryWriter CreateQueryWriterFromReader(this IWICImagingFactory imagingFactory, IWICMetadataQueryReader pIQueryReader, Guid? pguidVendor = null)
        {
            using (var pguidVendorPtr = CoTaskMemPtr.From(pguidVendor))
            {
                return imagingFactory.CreateQueryWriterFromReader(pIQueryReader, pguidVendorPtr);
            }
        }
    }
}
