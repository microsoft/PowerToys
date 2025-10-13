// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ImageResizer.Services
{
    public sealed class AiSuperResolutionContext
    {
        public AiSuperResolutionContext(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; }
    }
}
