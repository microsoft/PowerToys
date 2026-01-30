// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Media.Imaging;

namespace ImageResizer.Services
{
    public sealed class NoOpAiSuperResolutionService : IAISuperResolutionService
    {
        public static NoOpAiSuperResolutionService Instance { get; } = new NoOpAiSuperResolutionService();

        private NoOpAiSuperResolutionService()
        {
        }

        public BitmapSource ApplySuperResolution(BitmapSource source, int scale, string filePath)
        {
            return source;
        }

        public void Dispose()
        {
            // No resources to dispose in no-op implementation
        }
    }
}
