// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ImageResizer.Services
{
    /// <summary>
    /// Data model for AI capability cache file.
    /// </summary>
    internal sealed class AiCapabilityCache
    {
        public int Version { get; set; }

        public int State { get; set; }

        public string WindowsBuild { get; set; }

        public string Architecture { get; set; }

        public string Timestamp { get; set; }
    }
}
