// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

        private WinAiSuperResolutionService(ImageScaler imageScaler)
        {
            _imageScaler = imageScaler ?? throw new ArgumentNullException(nameof(imageScaler));
        }

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
                return AIFeatureReadyState.DisabledByUser;
            }
        }

        public static async Task<AIFeatureReadyResult> EnsureModelReadyAsync(IProgress<double> progress = null)
        {
            try
            {
                var operation = ImageScaler.EnsureReadyAsync();

                if (progress != null)
                {
                    operation.Progress = (asyncInfo, progressValue) =>
                    {
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

            try
            {
                var newWidth = source.PixelWidth * scale;
                var newHeight = source.PixelHeight * scale;

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

                (_imageScaler as IDisposable)?.Dispose();

                _disposed = true;
            }
        }
    }
}
