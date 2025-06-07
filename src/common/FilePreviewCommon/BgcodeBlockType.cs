// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.FilePreviewCommon
{
    public enum BgcodeBlockType
    {
        FileMetadataBlock = 0,
        GCodeBlock = 1,
        SlicerMetadataBlock = 2,
        PrinterMetadataBlock = 3,
        PrintMetadataBlock = 4,
        ThumbnailBlock = 5,
    }
}
