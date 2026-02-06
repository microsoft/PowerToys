// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Windows.Graphics.Imaging;

namespace ImageResizer.Services
{
    public sealed class WinAiSuperResolutionService : IAISuperResolutionService
    {
        private readonly ImageScaler _imageScaler;
        private readonly object _usageLock = new object();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WinAiSuperResolutionService"/> class.
        /// Private constructor. Use CreateAsync() factory method to create instances.
        /// </summary>
        private WinAiSuperResolutionService(ImageScaler imageScaler)
        {
            _imageScaler = imageScaler ?? throw new ArgumentNullException(nameof(imageScaler));
        }

        /// <summary>
        /// Async factory method to create and initialize WinAiSuperResolutionService.
        /// Returns null if initialization fails.
        /// </summary>
        public static async Task<WinAiSuperResolutionService> CreateAsync()
        {
            try
            {
                var imageScaler = await ImageScaler.CreateAsync();
                if (imageScaler == null)
                {
                    return null;
                }

                return new WinAiSuperResolutionService(imageScaler);
            }
            catch
            {
                return null;
            }
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

        public SoftwareBitmap ApplySuperResolution(SoftwareBitmap source, int scale, string filePath)
        {
            if (source == null || _disposed)
            {
                return source;
            }

            // Note: filePath parameter reserved for future use (e.g., logging, caching)
            // Currently not used by the ImageScaler API
            try
            {
                // Calculate target dimensions
                var newWidth = source.PixelWidth * scale;
                var newHeight = source.PixelHeight * scale;

                // Apply super resolution with thread-safe access
                // _usageLock protects concurrent access from Parallel.ForEachAsync threads
                SoftwareBitmap scaledBitmap;
                lock (_usageLock)
                {
                    if (_disposed)
                    {
                        return source;
                    }

                    scaledBitmap = _imageScaler.ScaleSoftwareBitmap(source, newWidth, newHeight);
                }

                return scaledBitmap ?? source;
            }
            catch (Exception)
            {
                // Any error, return original image gracefully
                return source;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_usageLock)
            {
                if (_disposed)
                {
                    return;
                }

                // ImageScaler implements IDisposable
                (_imageScaler as IDisposable)?.Dispose();

                _disposed = true;
            }
        }
    }
}
