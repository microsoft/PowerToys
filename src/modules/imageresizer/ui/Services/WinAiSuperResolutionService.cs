// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Windows.Graphics.Imaging;

namespace ImageResizer.Services
{
    public sealed class WinAiSuperResolutionService : IAISuperResolutionService
    {
        private readonly object _lock = new object();
        private ImageScaler _imageScaler;
        private bool _disposed;

        public WinAiSuperResolutionService()
        {
            // ImageScaler will be created by calling InitializeAsync()
            // This must be done on UI thread after checking model state
        }

        public static AIFeatureReadyState GetModelReadyState()
        {
            try
            {
                return ImageScaler.GetReadyState();
            }
            catch (Exception)
            {
                // If we can't get the state, treat it as disabled by user
                // The caller should check if it's Ready or NotReady
                return AIFeatureReadyState.DisabledByUser;
            }
        }

        public static async Task<AIFeatureReadyResult> EnsureModelReadyAsync(IProgress<double> progress = null)
        {
            try
            {
                var operation = ImageScaler.EnsureReadyAsync();

                // Register progress handler if provided
                if (progress != null)
                {
                    operation.Progress = (asyncInfo, progressValue) =>
                    {
                        // progressValue is a double representing completion percentage (0.0 to 1.0 or 0 to 100)
                        progress.Report(progressValue);
                    };
                }

                return await operation;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Initialize the ImageScaler instance. Must be called on UI thread after model is ready.
        /// Following the pattern from sample project.
        /// Thread-safe with double-checked locking pattern.
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            // First check without lock (performance optimization)
            if (_imageScaler != null)
            {
                return true; // Already initialized
            }

            lock (_lock)
            {
                // Double-check inside lock to prevent race condition
                if (_imageScaler != null)
                {
                    return true;
                }

                // Note: We cannot await inside a lock, so we release the lock,
                // perform the async work, then reacquire the lock to assign the result.
            }

            try
            {
                // Create ImageScaler instance
                // This must be called on UI thread in an async method
                // Note: Caller (InputViewModel) has already verified Ready state via Settings.CheckAiAvailability()
                var newImageScaler = await ImageScaler.CreateAsync();

                // Lock to safely assign the instance
                lock (_lock)
                {
                    // Check again in case another thread initialized while we were awaiting
                    if (_imageScaler == null)
                    {
                        _imageScaler = newImageScaler;
                    }
                }

                return _imageScaler != null;
            }
            catch (Exception)
            {
                // Failed to create ImageScaler
                lock (_lock)
                {
                    _imageScaler = null;
                }

                return false;
            }
        }

        public BitmapSource ApplySuperResolution(BitmapSource source, int scale, string filePath)
        {
            if (source == null || _disposed)
            {
                return source;
            }

            // Note: filePath parameter reserved for future use (e.g., logging, caching)
            // Currently not used by the ImageScaler API

            // Check if ImageScaler is initialized
            // If not, return original image (AI not enabled or initialization failed)
            if (_imageScaler == null)
            {
                return source;
            }

            try
            {
                // Convert WPF BitmapSource to WinRT SoftwareBitmap
                var softwareBitmap = ConvertBitmapSourceToSoftwareBitmap(source);
                if (softwareBitmap == null)
                {
                    return source;
                }

                // Calculate target dimensions
                var newWidth = softwareBitmap.PixelWidth * scale;
                var newHeight = softwareBitmap.PixelHeight * scale;

                // Apply super resolution with thread-safe access
                // Lock protects concurrent access from Parallel.ForEach threads
                SoftwareBitmap scaledBitmap;
                lock (_lock)
                {
                    if (_imageScaler == null)
                    {
                        // Double-check in case it was disposed
                        return source;
                    }

                    scaledBitmap = _imageScaler.ScaleSoftwareBitmap(softwareBitmap, newWidth, newHeight);
                }

                if (scaledBitmap == null)
                {
                    return source;
                }

                // Convert back to WPF BitmapSource
                return ConvertSoftwareBitmapToBitmapSource(scaledBitmap);
            }
            catch (Exception)
            {
                // Any error, return original image gracefully
                return source;
            }
        }

        private static SoftwareBitmap ConvertBitmapSourceToSoftwareBitmap(BitmapSource bitmapSource)
        {
            try
            {
                // Ensure the bitmap is in a compatible format
                var convertedBitmap = new FormatConvertedBitmap();
                convertedBitmap.BeginInit();
                convertedBitmap.Source = bitmapSource;
                convertedBitmap.DestinationFormat = PixelFormats.Bgra32;
                convertedBitmap.EndInit();

                int width = convertedBitmap.PixelWidth;
                int height = convertedBitmap.PixelHeight;
                int stride = width * 4; // 4 bytes per pixel for Bgra32
                byte[] pixels = new byte[height * stride];

                convertedBitmap.CopyPixels(pixels, stride, 0);

                // Create SoftwareBitmap from pixel data
                var softwareBitmap = new SoftwareBitmap(
                    BitmapPixelFormat.Bgra8,
                    width,
                    height,
                    BitmapAlphaMode.Premultiplied);

                using (var buffer = softwareBitmap.LockBuffer(BitmapBufferAccessMode.Write))
                using (var reference = buffer.CreateReference())
                {
                    unsafe
                    {
                        ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* dataInBytes, out uint capacity);
                        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, (IntPtr)dataInBytes, pixels.Length);
                    }
                }

                return softwareBitmap;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static BitmapSource ConvertSoftwareBitmapToBitmapSource(SoftwareBitmap softwareBitmap)
        {
            try
            {
                // Convert to Bgra8 format if needed
                var convertedBitmap = SoftwareBitmap.Convert(
                    softwareBitmap,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);

                int width = convertedBitmap.PixelWidth;
                int height = convertedBitmap.PixelHeight;
                int stride = width * 4; // 4 bytes per pixel for Bgra8
                byte[] pixels = new byte[height * stride];

                using (var buffer = convertedBitmap.LockBuffer(BitmapBufferAccessMode.Read))
                using (var reference = buffer.CreateReference())
                {
                    unsafe
                    {
                        ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* dataInBytes, out uint capacity);
                        System.Runtime.InteropServices.Marshal.Copy((IntPtr)dataInBytes, pixels, 0, pixels.Length);
                    }
                }

                // Create WPF BitmapSource from pixel data
                var wpfBitmap = BitmapSource.Create(
                    width,
                    height,
                    96, // DPI X
                    96, // DPI Y
                    PixelFormats.Bgra32,
                    null,
                    pixels,
                    stride);

                wpfBitmap.Freeze(); // Make it thread-safe
                return wpfBitmap;
            }
            catch (Exception)
            {
                return null;
            }
        }

        [ComImport]
        [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMemoryBufferByteAccess
        {
            unsafe void GetBuffer(out byte* buffer, out uint capacity);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                if (_imageScaler != null)
                {
                    // ImageScaler implements IDisposable
                    (_imageScaler as IDisposable)?.Dispose();
                    _imageScaler = null;
                }

                _disposed = true;
            }
        }
    }
}
